import Foundation

/// What a finished workout came to, read out the moment it is finished: how long, how much work,
/// what records fell, and how it compares with last time.
///
/// Built on the device from what it already holds, so the readout is the same whether the server
/// took the workout or the outbox did. It is built **before** the workout is handed over: the
/// previous performances are refreshed once the server has the session, and a readout built after
/// that would compare the workout with itself.
struct WorkoutSummary: Equatable, Sendable, Identifiable {
    let id: UUID
    /// The routine the workout was started from.
    let routine: WorkoutRoutine?
    let startedAt: Date
    /// From start to finish, for a live workout; for one written down afterwards, the span the
    /// athlete typed.
    let duration: TimeInterval
    let totals: WorkoutTotals
    /// How many sets beat a record standing when the workout began. The server decides what the
    /// session establishes; this is the device's reading of the same rule (`SetRecordOutcome`).
    let recordSets: Int
    /// The same figures for the last completed session of the same routine.
    let comparison: Comparison?
    /// Each movement trained, in the order it was, with today's best set against last time's.
    let movements: [MovementLine]
    /// The workout as a routine would hold it — see `routineMovements(of:)`.
    let routineMovements: [RoutineMovement]
    /// The completed session on the server, once it is known to be there — which is what saving
    /// the workout as a routine names, so the routine starts with it as its last session. `nil`
    /// while the workout waits in the outbox.
    let completedSessionId: UUID?

    /// The same readout, knowing the session the server completed.
    func completed(as sessionId: UUID?) -> WorkoutSummary {
        WorkoutSummary(
            id: id, routine: routine, startedAt: startedAt, duration: duration, totals: totals,
            recordSets: recordSets, comparison: comparison, movements: movements,
            routineMovements: routineMovements, completedSessionId: sessionId)
    }

    /// The last session of the routine, as the same figures.
    struct Comparison: Equatable, Sendable {
        let date: CalendarDay
        let duration: TimeInterval?
        let totals: WorkoutTotals
    }

    /// One movement: its working sets, and the best of them against the best last time.
    struct MovementLine: Equatable, Sendable, Identifiable {
        let movementDataId: UUID
        let workingSets: Int
        let best: TopSet?
        let previousBest: TopSet?
        let previousDate: CalendarDay?

        var id: UUID { movementDataId }

        /// The change in estimated one-rep max from last time's best set to today's, as a fraction:
        /// `0.05` is five percent up. Only between sets in one unit.
        var change: Double? {
            guard let best, let previousBest, best.unit == previousBest.unit, previousBest.estimate > 0
            else { return nil }
            return best.estimate / previousBest.estimate - 1
        }
    }

    /// A set by the numbers it is judged on.
    struct TopSet: Equatable, Sendable {
        let weight: Double
        let reps: Int
        let unit: WeightUnit
        /// What the estimate is taken over: the load moved where the weight is added to bodyweight
        /// and both sessions have a bodyweight to add, otherwise the weight as logged.
        let load: Double

        /// Epley, as the server reports it (`LiftPerformance.EstimatedOneRepMax`): `load × (1 +
        /// reps / 30)`, and the load itself at one rep.
        var estimate: Double { reps <= 1 ? load : load * (1 + Double(reps) / 30) }
    }

    /// Whether the workout trained its routine differently — another order, a movement added or
    /// dropped, a different number of working sets — which is when updating the routine is worth
    /// offering.
    var departsFromRoutine: Bool {
        guard let routine else { return false }
        return routineMovements != routine.movements
    }

    static func of(
        _ workout: ActiveWorkout,
        endingAt end: Date,
        previous: [UUID: PreviousPerformance],
        records: [RecordSubject: SubjectRecords],
        setups: [UUID: MovementSetup],
        bodyweight: BodyweightSeries,
        lastRoutineSession: TrainingSession.Completed?
    ) -> WorkoutSummary {
        let reading = WorkoutReading.of(workout)
        let weighing = bodyweight.inForce(on: CalendarDay(workout.startedAt))

        return WorkoutSummary(
            id: UUID(),
            routine: workout.routine,
            startedAt: workout.startedAt,
            duration: max(0, end.timeIntervalSince(workout.startedAt)),
            totals: reading.totals,
            recordSets: recordSets(in: workout, records: records),
            comparison: lastRoutineSession.map(comparison(with:)),
            movements: reading.movements
                .filter { !$0.sets.isEmpty }
                .map { movement in
                    let setup = setups[movement.movementDataId] ?? .none
                    let before = previous[movement.movementDataId]
                    // System load only where both sessions have a bodyweight in force: a weighted
                    // dip judged on bodyweight plus plate today and on the plate alone last time
                    // would read as a leap that never happened.
                    let weighedBefore = before.flatMap { bodyweight.inForce(on: $0.date) }
                    let bothWeighed = setup.addsToBodyweight && weighing != nil && weighedBefore != nil
                    return MovementLine(
                        movementDataId: movement.movementDataId,
                        workingSets: movement.workingSetCount,
                        best: best(
                            of: movement.sets.map(\.logged.command), setup: setup,
                            bodyweight: bothWeighed || before == nil ? weighing : nil),
                        previousBest: before.flatMap { performance in
                            best(of: performance.sets.compactMap(\.command), setup: setup,
                                 bodyweight: bothWeighed ? weighedBefore : nil)
                        },
                        previousDate: before?.date)
                },
            routineMovements: routineMovements(of: reading, routine: workout.routine),
            completedSessionId: nil)
    }

    /// The workout as a routine would hold it: every movement it still holds, in its order, with
    /// the working sets logged on it.
    ///
    /// A routine movement with nothing logged keeps the routine's own count — the athlete ran out
    /// of time or skipped it today, which is not a decision to drop it from the routine; taking it
    /// out of the workout is. A movement added today and never trained is left out, and so is one
    /// only warmed up on.
    private static func routineMovements(of reading: WorkoutReading, routine: WorkoutRoutine?) -> [RoutineMovement] {
        reading.movements.compactMap { movement in
            let sets = movement.workingSetCount > 0
                ? movement.workingSetCount
                : routine?.workingSets(of: movement.movementDataId) ?? 0
            guard sets > 0 else { return nil }
            return RoutineMovement(
                movementDataId: movement.movementDataId,
                workingSets: min(sets, RoutineMovement.setRange.upperBound))
        }
    }

    /// The sets that beat a standing record, each judged as the badge on its row is.
    private static func recordSets(in workout: ActiveWorkout, records: [RecordSubject: SubjectRecords]) -> Int {
        let commands = workout.sets.map(\.command)
        return commands.indices.count(where: { index in
            let command = commands[index]
            return SetRecordOutcome.of(
                command,
                after: Array(commands[..<index]),
                against: command.recordSubject.flatMap { records[$0] }
            ).isWorthSaying
        })
    }

    private static func comparison(with session: TrainingSession.Completed) -> Comparison {
        Comparison(
            date: session.envelope.date,
            duration: session.period.duration,
            totals: WorkoutTotals.of(commands: session.sets.compactMap(\.command)))
    }

    /// The working lift with the highest estimate, ties going to the heavier.
    private static func best(
        of commands: [SetCommand], setup: MovementSetup, bodyweight: BodyweightMeasurement?
    ) -> TopSet? {
        commands
            .compactMap(\.liftPart)
            .filter { $0.kind.isWork && $0.reps > 0 }
            .map { lift in
                TopSet(
                    weight: lift.weight, reps: lift.reps, unit: lift.unit,
                    load: SystemLoad.of(weight: lift.weight, unit: lift.unit, setup: setup, bodyweight: bodyweight)?
                        .total ?? lift.weight)
            }
            .max { ($0.estimate, $0.weight) < ($1.estimate, $1.weight) }
    }
}

extension SetEntry {
    /// The set as the athlete would state it, where it has a shape a client can state. Used to read
    /// a past session's sets with the arithmetic the workout in flight uses.
    var command: SetCommand? {
        switch self {
        case .lift(let set):
            .lift(SetCommand.Lift(
                movementDataId: set.movementDataId, reps: set.reps, weight: set.weight,
                unit: set.unit, rpe: set.rpe, kind: set.kind))
        case .distanceTime(let set):
            .distanceTime(SetCommand.DistanceTime(
                movementDataId: set.movementDataId, distance: set.distance, unit: set.unit,
                duration: set.duration, rpe: set.rpe))
        case .trailRun(let set):
            .trailRun(SetCommand.TrailRun(
                distance: set.distance, unit: set.unit, duration: set.duration,
                elevationGainMeters: set.elevationGainMeters, rpe: set.rpe))
        case .freeform(let common):
            .freeform(text: common.source.rawText ?? "")
        case .unrecognised:
            nil
        }
    }
}
