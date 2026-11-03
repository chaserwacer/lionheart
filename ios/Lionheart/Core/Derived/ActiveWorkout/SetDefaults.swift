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
/// 1. the last working set logged on this movement in this workout — what the athlete is actually
///    doing;
/// 2. the movement's next target — what they meant to do;
/// 3. last time on this movement, at the same working-set position — the "previous" column: the
///    weight carried from step 1 where there is one, the reps from last time, because the reps are
///    what a set to failure is trying to beat;
/// 4. an empty set of the right shape — a movement being logged for the first time ever.
///
/// Step 1 beats step 2 because a prescription is a plan and the last set is a fact: an athlete who
/// dropped the weight on set three does not want set four pre-filled with the plan they abandoned.
/// Step 2 beats step 3 because a plan written for today already knew about last time. A warm-up is
/// never a precedent: after a ramp the next set is the work, not one more warm-up.
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
        as modality: SetModality? = nil,
        previous: PreviousPerformance? = nil
    ) -> SetCommand {
        let (last, target, prior) = precedents(on: movementDataId, in: workout, previous: previous)

        switch modality ?? last?.modality ?? target.flatMap(Self.modality(of:))
            ?? prior.flatMap(Self.modality(of:)) ?? .lift {
        case .lift, .trailRun:
            // A trail run has no movement identity in the domain, so a movement's entry row cannot
            // produce one. Asking for it is a caller's mistake, and a lift is the harmless answer.
            return .lift(lift(
                on: movementDataId, from: last, orTarget: target, orPrior: prior,
                orLastUnitUsed: lastWeightUnit(in: workout)))
        case .distanceTime:
            return .distanceTime(distanceTime(
                on: movementDataId, from: last, orTarget: target, orPrior: prior,
                orLastUnitUsed: lastDistanceUnit(in: workout)))
        case .freeform:
            return .freeform(text: "")
        }
    }

    /// Whether the weight `nextSet` gives this movement is a value rather than a placeholder.
    ///
    /// A set already logged on it carries its weight, nought included: that is a bodyweight set
    /// that happened. A target carries one only when it names a load, because a lift target saved
    /// without a weight reads as nought and the two cannot be told apart. With neither, the nought
    /// is the empty set's placeholder.
    static func carriesWeightForward(
        on movementDataId: UUID, in workout: ActiveWorkout, previous: PreviousPerformance? = nil
    ) -> Bool {
        let (last, target, prior) = precedents(on: movementDataId, in: workout, previous: previous)
        if last != nil { return true }
        switch target {
        case .lift(let prescribed)?: return prescribed.targetWeight > 0
        case .distanceTime?, .freeform?, .unrecognised?: return false
        case nil:
            if case .lift? = prior { return true }
            return false
        }
    }

    /// Steps 1 to 3 of the rule: the last working set on this movement, its next target, and last
    /// time's working set at the position the next one will take.
    private static func precedents(
        on movementDataId: UUID, in workout: ActiveWorkout, previous: PreviousPerformance?
    ) -> (last: SetCommand?, target: SetPrescription?, prior: SetEntry?) {
        let work = workout.sets.filter { $0.movementDataId == movementDataId && $0.command.isWork }
        let target = WorkoutReading.of(workout)
            .movements.first { $0.movementDataId == movementDataId }?
            .nextTarget
        let prior = previous.flatMap { $0.movementDataId == movementDataId ? $0.workingSet(at: work.count) : nil }
        return (work.last?.command, target, prior)
    }

    /// Which entry shape last time's set was.
    private static func modality(of entry: SetEntry) -> SetModality? {
        switch entry {
        case .lift: .lift
        case .distanceTime: .distanceTime
        case .trailRun, .freeform, .unrecognised: nil
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
        orPrior prior: SetEntry?,
        orLastUnitUsed unit: WeightUnit?
    ) -> SetCommand.Lift {
        if case .lift(let set)? = last {
            // Today's weight, and — where no plan says otherwise — last time's reps at this
            // position, which is the number this set is trying to beat.
            let priorReps: Int? = if target == nil, case .lift(let before)? = prior { before.reps } else { nil }
            return SetCommand.Lift(
                movementDataId: movementDataId,
                reps: priorReps ?? set.reps,
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
        if case .lift(let before)? = prior {
            return SetCommand.Lift(
                movementDataId: movementDataId,
                reps: before.reps,
                weight: before.weight,
                unit: before.unit,
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
        orPrior prior: SetEntry?,
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
        if case .distanceTime(let before)? = prior {
            return SetCommand.DistanceTime(
                movementDataId: movementDataId,
                distance: before.distance,
                unit: before.unit,
                duration: before.duration,
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
