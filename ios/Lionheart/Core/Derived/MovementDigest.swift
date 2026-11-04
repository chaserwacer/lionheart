import Foundation

/// One movement of a session in a line, the way a lifter writes it in a log book.
///
/// Straight sets at one load read as the load and every set's reps — `330 lb × 6, 5, 5, 4` — because
/// where the reps fell off is the information. Sets at changing loads read as the heaviest set and
/// the count, since listing every pairing would be the detail screen again.
struct MovementDigest: Equatable, Sendable, Identifiable {
    let order: Int
    /// The movement the line is about; absent for a trail run and the "Other work" line, which name none.
    let movementDataId: UUID?
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
        /// A trail run: how far, how long, and the climb.
        case run(distance: Double, unit: DistanceUnit, duration: TimeInterval, climbMeters: Double)
    }

    /// The heaviest set of a movement, as performed.
    struct TopSet: Equatable, Sendable {
        let weight: Double
        let unit: WeightUnit
        let reps: Int
    }

    /// The movements of a set list, in the order they were performed, then each trail run on a line
    /// of its own — a run imported from Apple Health is a session of exactly one — then any other
    /// work that names no movement, a freeform set, as one counted line, so no set goes unmentioned.
    static func of(_ sets: [SetEntry], catalogue: MovementCatalogue) -> [MovementDigest] {
        let detail = SessionDetail.of(sets, catalogue: catalogue)
        let movements = detail.blocks.map { block in
            MovementDigest(
                order: block.order, movementDataId: block.movementDataId, movementName: block.movementName,
                line: line(of: block.sets.map(\.entry)))
        }
        let runs = detail.unattached.compactMap { set -> Line? in
            guard case .trailRun(let run) = set.entry else { return nil }
            return .run(distance: run.distance, unit: run.unit, duration: run.duration, climbMeters: run.elevationGainMeters)
        }
        let ranLines = runs.enumerated().map { index, run in
            MovementDigest(order: movements.count + index, movementDataId: nil, movementName: "Trail run", line: run)
        }
        let others = detail.unattached.count - runs.count
        guard others > 0 else { return movements + ranLines }
        return movements + ranLines + [MovementDigest(
            order: movements.count + ranLines.count, movementDataId: nil, movementName: "Other work",
            line: .counted(setCount: others))]
    }

    /// A movement's line reads its work: a warm-up ramp before two sets at one load is still
    /// straight sets at that load.
    static func line(of all: [SetEntry]) -> Line {
        let entries = all.work.isEmpty ? all : all.work
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
