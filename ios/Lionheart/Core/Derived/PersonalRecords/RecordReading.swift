import Foundation

/// One subject's standing records, arranged the way a lifter or a runner reads them.
///
/// **Lifting records are read one weight unit at a time.** A movement logged in both units holds a
/// kilograms record and a pounds record of each kind, and the two never compare, so they are two
/// separate readings — never one list where the second unit's heaviest set silently replaces the
/// first's. The order is most recently set first, which is the unit the athlete is lifting in now.
///
/// Distances carry no unit in a record's identity (they rank in meters), so endurance is one
/// reading whatever units the efforts were logged in.
struct RecordReading: Equatable, Sendable {
    let subject: RecordSubject
    let lifting: [Lifting]
    let endurance: Endurance?
    /// Records whose kind this build does not know. Kept so the count on screen matches the
    /// server's, and so nothing the athlete holds is invisible.
    let unrecognised: [PersonalRecord]

    /// One weight unit's lifting records on a subject.
    struct Lifting: Equatable, Sendable, Identifiable {
        let unit: WeightUnit
        let heaviestSet: PersonalRecord?
        let bestSetVolume: PersonalRecord?
        let bestSessionVolume: PersonalRecord?
        let grid: RepMaxGrid
        /// The record implying the heaviest single, by the server's own estimates — a selection
        /// among figures the server sent, not an estimate made here. Absent when that record is
        /// itself a single: a weight actually lifted once is no estimate, and the heaviest set
        /// already says it.
        let strongestEstimate: PersonalRecord?
        /// When the newest of these was set, which orders the units.
        let lastAchievedAt: Date

        var id: WeightUnit { unit }
    }

    /// A subject's distance records.
    struct Endurance: Equatable, Sendable {
        let longestDistance: PersonalRecord?
        let bestPace: PersonalRecord?
        /// One per distance run, shortest first — the order a runner reads their times in.
        let fastestTimes: [PersonalRecord]
    }

    /// The record that stands for the whole subject in a list: the heaviest set in the unit lifted
    /// in most recently, else the longest distance, else whatever the subject holds.
    var headline: PersonalRecord? {
        lifting.first?.heaviestSet
            ?? lifting.first?.bestSetVolume
            ?? lifting.first?.grid.cells.compactMap(\.record).last
            ?? endurance?.longestDistance
            ?? endurance?.fastestTimes.last
            ?? unrecognised.first
    }

    static func of(_ held: SubjectRecords) -> RecordReading {
        let shelved = Dictionary(grouping: held.records, by: Shelf.of)
        let distances = shelved[.endurance] ?? []

        return RecordReading(
            subject: held.subject,
            lifting: shelved
                .compactMap { shelf, records in
                    guard case .lifting(let unit) = shelf else { return nil }
                    return lifting(records, in: unit)
                }
                .sorted { $0.lastAchievedAt > $1.lastAchievedAt },
            endurance: distances.isEmpty ? nil : endurance(distances),
            unrecognised: shelved[.unrecognised] ?? [])
    }

    /// Which part of a reading a record belongs in. The weight unit is part of the lifting shelf
    /// because it is part of the record's identity.
    private enum Shelf: Hashable {
        case lifting(WeightUnit)
        case endurance
        case unrecognised

        static func of(_ record: PersonalRecord) -> Shelf {
            switch record.achievement {
            case .heaviestSet, .repMax, .bestSetVolume, .bestSessionVolume:
                record.achievement.heldIn.map(Shelf.lifting) ?? .unrecognised
            case .longestDistance, .fastestTime, .bestPace:
                .endurance
            case .unrecognised:
                .unrecognised
            }
        }
    }

    private static func lifting(_ records: [PersonalRecord], in unit: WeightUnit) -> Lifting {
        func newest(_ kind: RecordKind) -> PersonalRecord? {
            records.filter { $0.achievement.kind == kind }.reduce(nil, newer)
        }

        let repMaxes = records.filter {
            if case .repMax = $0.achievement { true } else { false }
        }
        let estimated = records.filter { $0.achievement.estimatedOneRepMax != nil }
        let heaviestSet = newest(.heaviestSet)

        return Lifting(
            unit: unit,
            heaviestSet: heaviestSet,
            bestSetVolume: newest(.bestSetVolume),
            bestSessionVolume: newest(.bestSessionVolume),
            grid: RepMaxGrid.of(heaviestSet: heaviestSet, repMaxes: repMaxes),
            strongestEstimate: estimated
                .max {
                    ($0.achievement.estimatedOneRepMax ?? 0) < ($1.achievement.estimatedOneRepMax ?? 0)
                }
                .flatMap { best in isSingle(best) ? nil : best },
            lastAchievedAt: records.map(\.achievedAt).max() ?? .distantPast)
    }

    private static func isSingle(_ record: PersonalRecord) -> Bool {
        record.achievement.performedSet.map { $0.reps <= 1 } ?? false
    }

    private static func endurance(_ records: [PersonalRecord]) -> Endurance {
        let fastest = records.reduce(into: [Int: PersonalRecord]()) { held, record in
            guard case .fastestTime(let meters) = record.achievement.kind else { return }
            held[meters] = newer(held[meters], record)
        }

        return Endurance(
            longestDistance: records.filter { $0.achievement.kind == .longestDistance }
                .reduce(nil, newer),
            bestPace: records.filter { $0.achievement.kind == .bestPace }.reduce(nil, newer),
            fastestTimes: fastest.sorted { $0.key < $1.key }.map(\.value))
    }

    /// Of two records of one identity, the one set later.
    ///
    /// Only one record of an identity should stand at a time, but the server keeps no unique index
    /// over standing records (SQLite cannot defer one through a hand-over), so a reading picks
    /// deterministically rather than by whichever arrived first.
    static func newer(_ held: PersonalRecord?, _ candidate: PersonalRecord) -> PersonalRecord {
        guard let held, held.achievedAt >= candidate.achievedAt else { return candidate }
        return held
    }
}
