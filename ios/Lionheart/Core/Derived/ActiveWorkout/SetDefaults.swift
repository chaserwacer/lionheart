import Foundation

/// What the entry row opens with.
///
/// This is the rule that decides whether logging is fast. Lifting is sets of the same weight for
/// the same reps, so the second set of five at a hundred kilos should cost one tap, not six; the
/// values the athlete last used on *this movement* are therefore the default, and the prescription
/// is the fallback only where nothing has been logged yet.
///
/// The order is deliberate and is the whole of the rule:
///
/// 1. the last set logged on this movement in this workout — what the athlete is actually doing;
/// 2. the movement's next target — what they meant to do;
/// 3. an empty set of the right shape — a movement being logged for the first time ever.
///
/// Step 1 beats step 2 because a prescription is a plan and the last set is a fact: an athlete who
/// dropped the weight on set three does not want set four pre-filled with the plan they abandoned.
enum SetDefaults {
    /// The set the entry row starts from for `movementDataId`.
    ///
    /// - Parameter modality: forced when the athlete has picked a shape, inferred otherwise.
    ///   Inference follows the same order as the values: the last set's shape, then the target's,
    ///   then `.lift` — which is the right guess because `MovementData` carries no modality and
    ///   lifting is what the overwhelming majority of movements are.
    static func nextSet(
        on movementDataId: UUID,
        in workout: ActiveWorkout,
        as modality: SetModality? = nil
    ) -> SetCommand {
        let last = workout.sets.last { $0.movementDataId == movementDataId }?.command
        let target = WorkoutReading.of(workout)
            .movements.first { $0.movementDataId == movementDataId }?
            .nextTarget

        switch modality ?? last?.modality ?? target.flatMap(Self.modality(of:)) ?? .lift {
        case .lift, .trailRun:
            // A trail run has no movement identity in the domain, so a movement's entry row cannot
            // produce one. Asking for it is a caller's mistake, and a lift is the harmless answer.
            return .lift(lift(
                on: movementDataId, from: last, orTarget: target,
                orLastUnitUsed: lastWeightUnit(in: workout)))
        case .distanceTime:
            return .distanceTime(distanceTime(
                on: movementDataId, from: last, orTarget: target,
                orLastUnitUsed: lastDistanceUnit(in: workout)))
        case .freeform:
            return .freeform(text: "")
        }
    }

    /// The weight unit the athlete has been using anywhere in this workout.
    ///
    /// Carried across movements because the unit is a property of the athlete, not of the exercise:
    /// someone lifting in pounds does so for all of them, and making them change it on every new
    /// movement is the kind of friction that gets an app abandoned.
    private static func lastWeightUnit(in workout: ActiveWorkout) -> WeightUnit? {
        workout.sets.compactMap(\.command.liftPart).last?.unit
            ?? workout.prescriptions.compactMap(Self.prescribedWeightUnit).last
    }

    private static func lastDistanceUnit(in workout: ActiveWorkout) -> DistanceUnit? {
        workout.sets.compactMap(\.command.coveredPart).last?.unit
    }

    private static func prescribedWeightUnit(_ target: SetPrescription) -> WeightUnit? {
        switch target {
        case .lift(let prescribed): prescribed.unit
        case .distanceTime, .freeform, .unrecognised: nil
        }
    }

    /// The trail run the entry row starts from: the last one logged, else an empty one.
    ///
    /// Separate from `nextSet(on:…)` because a trail run belongs to no movement — there is no id to
    /// ask about, and the only history that bears on it is the previous trail run of this workout.
    static func nextTrailRun(in workout: ActiveWorkout) -> SetCommand {
        guard case .trailRun(let last)? = workout.sets.last(where: { $0.modality == .trailRun })?.command
        else {
            return .trailRun(SetCommand.TrailRun(
                distance: 0, unit: .kilometers, duration: 0, elevationGainMeters: 0, rpe: .unrated))
        }
        return .trailRun(last)
    }

    private static func lift(
        on movementDataId: UUID,
        from last: SetCommand?,
        orTarget target: SetPrescription?,
        orLastUnitUsed unit: WeightUnit?
    ) -> SetCommand.Lift {
        if case .lift(let set)? = last {
            return SetCommand.Lift(
                movementDataId: movementDataId,
                reps: set.reps,
                weight: set.weight,
                unit: set.unit,
                rpe: set.rpe)
        }
        if case .lift(let prescribed)? = target {
            return SetCommand.Lift(
                movementDataId: movementDataId,
                reps: prescribed.targetReps,
                weight: prescribed.targetWeight,
                unit: prescribed.unit,
                rpe: .unrated)
        }
        return SetCommand.Lift(
            movementDataId: movementDataId,
            reps: 0,
            weight: 0,
            unit: unit ?? .kilograms,
            rpe: .unrated)
    }

    private static func distanceTime(
        on movementDataId: UUID,
        from last: SetCommand?,
        orTarget target: SetPrescription?,
        orLastUnitUsed unit: DistanceUnit?
    ) -> SetCommand.DistanceTime {
        if case .distanceTime(let set)? = last {
            return SetCommand.DistanceTime(
                movementDataId: movementDataId,
                distance: set.distance,
                unit: set.unit,
                duration: set.duration,
                rpe: set.rpe)
        }
        if case .distanceTime(let prescribed)? = target {
            return SetCommand.DistanceTime(
                movementDataId: movementDataId,
                distance: prescribed.targetDistance,
                unit: prescribed.unit,
                duration: prescribed.targetDuration,
                rpe: .unrated)
        }
        return SetCommand.DistanceTime(
            movementDataId: movementDataId,
            distance: 0,
            unit: unit ?? .kilometers,
            duration: 0,
            rpe: .unrated)
    }

    /// Which entry shape a target asks for. A freeform target asks for nothing in particular, so
    /// it does not steer the shape — the athlete's own history or `.lift` does.
    private static func modality(of target: SetPrescription) -> SetModality? {
        switch target {
        case .lift: .lift
        case .distanceTime: .distanceTime
        case .freeform, .unrecognised: nil
        }
    }
}
