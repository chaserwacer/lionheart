import Foundation

/// One movement of a session in a line, the way a lifter writes it in a log book.
///
/// Straight sets at one load read as the load and every set's reps — `330 lb × 6, 5, 5, 4` — because
/// where the reps fell off is the information. Sets at changing loads read as the heaviest set and
/// the count, since listing every pairing would be the detail screen again.
struct MovementDigest: Equatable, Sendable, Identifiable {
    let order: Int
    let movementName: String
    let line: Line

    var id: Int { order }

    enum Line: Equatable, Sendable {
        /// Every set a lift at the same load and unit.
        case straight(weight: Double, unit: WeightUnit, reps: [Int])
        /// Lifts at more than one load: the heaviest set, and how many there were.
        case ramped(top: TopSet, setCount: Int)
        /// Sets with no single load to state — timed work, or a mix of shapes.
        case counted(setCount: Int)
    }

    /// The heaviest set of a movement, as performed.
    struct TopSet: Equatable, Sendable {
        let weight: Double
        let unit: WeightUnit
        let reps: Int
    }

    /// The movements of a set list, in the order they were performed.
    static func of(_ sets: [SetEntry], catalogue: MovementCatalogue) -> [MovementDigest] {
        SessionDetail.of(sets, catalogue: catalogue).blocks.map { block in
            MovementDigest(order: block.order, movementName: block.movementName, line: line(of: block.sets.map(\.entry)))
        }
    }

    static func line(of entries: [SetEntry]) -> Line {
        let lifts = entries.compactMap { entry -> (weight: Double, unit: WeightUnit, reps: Int)? in
            guard case .lift(let lift) = entry else { return nil }
            return (lift.weight, lift.unit, lift.reps)
        }
        guard lifts.count == entries.count, let first = lifts.first else {
            return .counted(setCount: entries.count)
        }
        if lifts.allSatisfy({ $0.weight == first.weight && $0.unit == first.unit }) {
            return .straight(weight: first.weight, unit: first.unit, reps: lifts.map(\.reps))
        }
        let sameUnit = lifts.filter { $0.unit == first.unit }
        guard sameUnit.count == lifts.count,
              let top = sameUnit.max(by: { ($0.weight, $0.reps) < ($1.weight, $1.reps) })
        else { return .counted(setCount: entries.count) }
        return .ramped(
            top: TopSet(weight: top.weight, unit: top.unit, reps: top.reps),
            setCount: entries.count)
    }
}
