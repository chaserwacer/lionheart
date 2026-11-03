import Foundation

/// One record chain, read for a screen: every record of one identity, newest first — the one
/// standing, the ones it beat, and any withdrawn.
///
/// The history route takes no weight unit and answers every unit's chain of a kind, so the chain
/// is narrowed here to the record's own key. Without that a pounds five-rep max would list the
/// kilograms ones among the records it "beat", and the trend line would jump between scales.
///
/// Each link says how long the record before it stood from its own lineage, rather than by walking
/// the chain: the server copies the predecessor's date onto the record for exactly that, and
/// records migrated from before this system do not always form a single line.
struct RecordHistory: Equatable, Sendable {
    let key: RecordKey
    let links: [PersonalRecord]
    /// The links that stood, oldest first, when there are enough of them to be a line and they
    /// share one measure. Empty otherwise — one point is not a trend.
    let trend: [TrendPoint]

    struct TrendPoint: Equatable, Sendable, Identifiable {
        let record: PersonalRecord
        let value: Double
        let measure: TrendMeasure

        var id: UUID { record.id }
        var date: Date { record.achievedAt }
    }

    /// What a trend's numbers are, so a chart can label its axis. A time is seconds — a fastest
    /// time, or a pace per kilometer.
    enum TrendMeasure: Hashable, Sendable {
        case weight(WeightUnit)
        case distance(DistanceUnit)
        case time
    }

    /// - Parameter chain: the history route's answer, newest first. Its order is kept: two records
    ///   can share an instant, and the server breaks that tie by the order they were written.
    static func reading(_ chain: [PersonalRecord], of key: RecordKey) -> RecordHistory {
        let links = chain.filter { $0.key == key }

        // A withdrawn record never stood in the sense the line means, so it is not a point on it.
        let points = links.reversed().compactMap { record -> TrendPoint? in
            switch record.standing {
            case .standing, .beaten: trendPoint(record)
            case .retracted, .unrecognised: nil
            }
        }
        let isOneMeasure = Set(points.map(\.measure)).count == 1

        return RecordHistory(
            key: key,
            links: links,
            trend: points.count >= 2 && isOneMeasure && isProgression(links) ? points : [])
    }

    /// Whether the chain reads as one line of progress: the newest record that stood is the one
    /// standing now. Records migrated from before this system do not always — a movement can hold
    /// a standing first record with newer beaten ones behind it — and a step line through those
    /// would show the newest figure as the one that stands.
    private static func isProgression(_ links: [PersonalRecord]) -> Bool {
        let newestThatStood = links.first {
            switch $0.standing {
            case .standing, .beaten: true
            case .retracted, .unrecognised: false
            }
        }
        return newestThatStood?.standing == .standing
    }

    /// Whether any link was withdrawn, which is what a screen explains retraction for.
    var hasRetractions: Bool { links.contains { $0.standing == .retracted } }

    /// The number a record ranks by, on the scale it was measured in.
    ///
    /// A longest distance is plotted in the unit it was logged in, which is why a chain that
    /// changed distance unit has no line: converting here would be a second place the server's
    /// distance arithmetic lived.
    private static func trendPoint(_ record: PersonalRecord) -> TrendPoint? {
        let measured: (Double, TrendMeasure)? = switch record.achievement {
        case .heaviestSet(let lift), .repMax(let lift): (lift.weight, .weight(lift.unit))
        case .bestSetVolume(let set): (set.volume, .weight(set.unit))
        case .bestSessionVolume(let session): (session.volume, .weight(session.unit))
        case .longestDistance(let effort): (effort.distance, .distance(effort.unit))
        case .fastestTime(_, let effort): (effort.duration, .time)
        case .bestPace(let pace): (pace.perKilometer, .time)
        case .unrecognised: nil
        }
        return measured.map { TrendPoint(record: record, value: $0.0, measure: $0.1) }
    }
}

extension PersonalRecord {
    /// Whole days the record this one replaced had stood, or `nil` for the first of its kind.
    var daysPredecessorStood: Int? {
        switch lineage {
        case .firstOfItsKind: nil
        case .replaced(let previous): Self.wholeDays(from: previous, to: achievedAt)
        }
    }

    /// Whole days this record has stood as of `now`, or `nil` when it no longer stands — a beaten
    /// record's span is told by the record that beat it.
    func daysStanding(asOf now: Date) -> Int? {
        switch standing {
        case .standing: Self.wholeDays(from: achievedAt, to: now)
        case .beaten, .retracted, .unrecognised: nil
        }
    }

    private static func wholeDays(from start: Date, to end: Date) -> Int {
        max(0, Int(end.timeIntervalSince(start) / 86_400))
    }
}
