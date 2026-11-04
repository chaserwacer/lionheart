import Foundation

/// One measurement of the score against its target, as a row states it.
struct ScoreMeasure: Equatable, Sendable, Identifiable {
    enum Unit: Equatable, Sendable { case hardSets, minutes, sessions }

    let label: String
    let measured: Double
    let target: Double
    let unit: Unit

    var id: String { label }
    /// The measurement's share of its target, capped at the whole of it — what the score pays.
    var share: Double { target > 0 ? min(1, measured / target) : 0 }
}

/// The score's measurements, part by part, so the screen states "push 6 of 8 hard sets" rather than
/// a sub-score nobody can check.
enum ScoreBreakdown {
    static func lifting(_ part: LiftingPart) -> [ScoreMeasure] {
        part.families.map {
            ScoreMeasure(label: $0.family.name, measured: Double($0.hardSets), target: Double($0.target), unit: .hardSets)
        }
    }

    /// Zone 2 and zones 4–5 when measured; nothing when the measure is unknown, which the screen
    /// states as unknown rather than as zero.
    static func cardio(_ measure: CardioMeasure) -> [ScoreMeasure] {
        guard case .measured(let zone2, let zone2Target, let high, let highTarget, _, _) = measure else { return [] }
        return [
            ScoreMeasure(label: "Zone 2", measured: zone2, target: Double(zone2Target), unit: .minutes),
            ScoreMeasure(label: "Zones 4–5", measured: high, target: Double(highTarget), unit: .minutes),
        ]
    }

    /// The window first, then each block before it — "this week", "1 week ago", and so on.
    static func consistency(_ part: ConsistencyPart) -> [ScoreMeasure] {
        part.sessionsPerBlock.enumerated().map { index, sessions in
            ScoreMeasure(
                label: index == 0 ? "Last 7 days" : "\(index * 7)–\(index * 7 + 6) days ago",
                measured: Double(sessions), target: Double(part.target), unit: .sessions)
        }
    }
}
