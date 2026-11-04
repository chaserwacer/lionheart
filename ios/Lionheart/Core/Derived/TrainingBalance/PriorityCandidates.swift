import Foundation

/// The lifts a family's priority can be chosen from: the athlete's compound movements of that
/// family, and the unclassified ones, which the server takes at his word.
///
/// Isolation work of the family is left out — a curl is pull work, but nobody builds a block around
/// one — and so is any movement classified into another family, which the server would refuse.
enum PriorityCandidates {
    struct Choice: Equatable, Sendable {
        let fitting: [MovementData]
        let unclassified: [MovementData]
    }

    static func of(_ movements: [MovementData], for family: PatternFamily) -> Choice {
        let byName: (MovementData, MovementData) -> Bool = {
            $0.displayName.localizedCaseInsensitiveCompare($1.displayName) == .orderedAscending
        }
        let fitting = movements.filter { movement in
            guard let trains = movement.classification.classification else { return false }
            return trains.isCompound && movement.classification.family == family
        }
        let unclassified = movements.filter { movement in
            switch movement.classification {
            case .unclassified: true
            case .classified, .unrecognised: false
            }
        }
        return Choice(fitting: fitting.sorted(by: byName), unclassified: unclassified.sorted(by: byName))
    }
}
