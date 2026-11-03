import Foundation

/// How a priority lift's week reads against the twelve weeks before: the verdict a row states, with
/// the figures behind it.
///
/// The bands are the score's (`ScoreRules` on the server): above the prior best is a new best,
/// within five per cent of it is held, further under is below. Mirrored here only to name the band;
/// the points are the server's.
enum LiftStanding: Equatable, Sendable {
    case notTrained
    /// Trained, but no set estimates a single: all past twelve reps, or at no load.
    case noEstimate
    /// Trained with nothing in the twelve weeks before to compare against.
    case firstBack(best: Double, unit: WeightUnit)
    /// `change` is the fraction above or below the prior best: 0.04 is four per cent up.
    case newBest(best: Double, prior: Double, change: Double, isLifetime: Bool, unit: WeightUnit)
    case held(best: Double, prior: Double, change: Double, unit: WeightUnit)
    case below(best: Double, prior: Double, change: Double, unit: WeightUnit)
    case unreadable

    /// A best within this fraction under the prior best is held.
    static let heldWithin = 0.05

    static func of(_ progress: LiftProgress) -> LiftStanding {
        switch progress {
        case .notTrained:
            return .notTrained
        case .noEstimate:
            return .noEstimate
        case .firstBack(let best, let unit, _):
            return .firstBack(best: best, unit: unit)
        case .compared(let best, let prior, let allTime, let unit, _):
            let change = prior > 0 ? best / prior - 1 : 0
            if best > prior { return .newBest(best: best, prior: prior, change: change, isLifetime: best > allTime, unit: unit) }
            if best >= (1 - heldWithin) * prior { return .held(best: best, prior: prior, change: change, unit: unit) }
            return .below(best: best, prior: prior, change: change, unit: unit)
        case .unrecognised:
            return .unreadable
        }
    }

    /// The word a row leads with.
    var verdict: String {
        switch self {
        case .notTrained: "Not trained this week"
        case .noEstimate: "Trained, no estimate"
        case .firstBack: "First in 12 weeks"
        case .newBest(_, _, _, let isLifetime, _): isLifetime ? "Lifetime best" : "12-week best"
        case .held: "Held"
        case .below: "Below"
        case .unreadable: "Not readable"
        }
    }
}

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
