import Foundation

/// How a priority lift's week reads against the twelve weeks before: the verdict a row states, with
/// the figures behind it. The band is the server's (`ProgressStanding`); this only pairs it with the
/// change a row prints.
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

    static func of(_ progress: LiftProgress) -> LiftStanding {
        switch progress {
        case .notTrained:
            return .notTrained
        case .noEstimate:
            return .noEstimate
        case .firstBack(let best, let unit, _):
            return .firstBack(best: best, unit: unit)
        case .compared(let best, let prior, let standing, let unit, _):
            let change = prior > 0 ? best / prior - 1 : 0
            return switch standing {
            case .lifetimeBest: .newBest(best: best, prior: prior, change: change, isLifetime: true, unit: unit)
            case .newBest: .newBest(best: best, prior: prior, change: change, isLifetime: false, unit: unit)
            case .held: .held(best: best, prior: prior, change: change, unit: unit)
            case .below: .below(best: best, prior: prior, change: change, unit: unit)
            case .unrecognised: .unreadable
            }
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
