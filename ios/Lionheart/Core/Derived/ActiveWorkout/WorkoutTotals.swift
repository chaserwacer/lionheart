import Foundation

/// What the workout adds up to so far.
///
/// Derived on the device, which is an exception to "the server already derives it" and earns it the
/// same way the rest clock does: the figure has to change the instant a set is logged, including
/// with no signal. It mirrors the server's own arithmetic — `PersonalRecord.Volume` is
/// `Weight * Reps` — so the running total and the completed session's total are the same quantity.
///
/// Weight is totalled **per unit** rather than converted into one. The server compares a record's
/// weight numerically without converting units, so a device-side conversion would produce a volume
/// that disagrees with the record sitting next to it. Mixing units inside one workout is rare
/// enough that showing both totals beats inventing a reconciliation the server does not make.
struct WorkoutTotals: Equatable, Sendable {
    let setCount: Int
    /// Lift volume per weight unit, ordered by the unit's first appearance in the workout.
    let weights: [WeightTotal]
    /// Ground covered per distance unit, ordered by first appearance.
    let distances: [DistanceTotal]
    /// Seconds spent in sets that record a duration. Not the session's elapsed time, which includes
    /// the rests and is the workout's own clock.
    let movingDuration: TimeInterval

    struct WeightTotal: Equatable, Sendable, Identifiable {
        let unit: WeightUnit
        /// Σ weight × reps.
        let volume: Double
        let reps: Int
        let setCount: Int

        var id: Int { unit.wire }
    }

    struct DistanceTotal: Equatable, Sendable, Identifiable {
        let unit: DistanceUnit
        let distance: Double
        let setCount: Int

        var id: Int { unit.wire }
    }

    var isEmpty: Bool { setCount == 0 }

    static func of(_ sets: [LoggedSet]) -> WorkoutTotals {
        let lifts = sets.compactMap(\.command.liftPart)
        let covered = sets.compactMap(\.command.coveredPart)

        return WorkoutTotals(
            setCount: sets.count,
            weights: weightTotals(of: lifts),
            distances: distanceTotals(of: covered),
            movingDuration: covered.reduce(0) { $0 + $1.duration })
    }

    /// A finished session's sets, totalled by the same arithmetic as the workout in flight, so a
    /// session reads the same total in the log as it did while it was being logged.
    static func of(entries: [SetEntry]) -> WorkoutTotals {
        let lifts = entries.compactMap { entry -> SetCommand.Lift? in
            guard case .lift(let lift) = entry else { return nil }
            return SetCommand.Lift(
                movementDataId: lift.movementDataId, reps: lift.reps, weight: lift.weight,
                unit: lift.unit, rpe: lift.rpe)
        }
        let covered = entries.compactMap { entry -> GroundCovered? in
            switch entry {
            case .distanceTime(let set):
                GroundCovered(distance: set.distance, unit: set.unit, duration: set.duration)
            case .trailRun(let set):
                GroundCovered(distance: set.distance, unit: set.unit, duration: set.duration)
            case .lift, .freeform, .unrecognised:
                nil
            }
        }
        return WorkoutTotals(
            setCount: entries.count,
            weights: weightTotals(of: lifts),
            distances: distanceTotals(of: covered),
            movingDuration: covered.reduce(0) { $0 + $1.duration })
    }

    private static func weightTotals(of lifts: [SetCommand.Lift]) -> [WeightTotal] {
        let byUnit = Dictionary(grouping: lifts, by: \.unit.wire)
        return firstAppearances(of: lifts.map(\.unit.wire)).compactMap { key in
            guard let group = byUnit[key], let unit = group.first?.unit else { return nil }
            return WeightTotal(
                unit: unit,
                volume: group.reduce(0) { $0 + $1.weight * Double($1.reps) },
                reps: group.reduce(0) { $0 + $1.reps },
                setCount: group.count)
        }
    }

    private static func distanceTotals(of covered: [GroundCovered]) -> [DistanceTotal] {
        let byUnit = Dictionary(grouping: covered, by: \.unit.wire)
        return firstAppearances(of: covered.map(\.unit.wire)).compactMap { key in
            guard let group = byUnit[key], let unit = group.first?.unit else { return nil }
            return DistanceTotal(
                unit: unit,
                distance: group.reduce(0) { $0 + $1.distance },
                setCount: group.count)
        }
    }

    /// The keys in the order they first appear, each once.
    private static func firstAppearances(of keys: [Int]) -> [Int] {
        keys.reduce(into: []) { order, key in
            if !order.contains(key) { order.append(key) }
        }
    }
}

/// The part of a set that covers ground, whichever shape it was logged in.
///
/// A distance-time set and a trail run are different shapes with different identities, and they
/// contribute the same two figures to a total. This is what lets the arithmetic be written once
/// without pretending the two set shapes are one.
struct GroundCovered: Equatable, Sendable {
    let distance: Double
    let unit: DistanceUnit
    let duration: TimeInterval
}

extension SetCommand {
    /// The lift inside this set, where it is one.
    var liftPart: Lift? {
        switch self {
        case .lift(let set): set
        case .distanceTime, .trailRun, .freeform: nil
        }
    }

    /// The ground this set covered, where it covered any.
    var coveredPart: GroundCovered? {
        switch self {
        case .distanceTime(let set):
            GroundCovered(distance: set.distance, unit: set.unit, duration: set.duration)
        case .trailRun(let set):
            GroundCovered(distance: set.distance, unit: set.unit, duration: set.duration)
        case .lift, .freeform:
            nil
        }
    }
}
