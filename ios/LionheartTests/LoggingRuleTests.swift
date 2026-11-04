import Foundation
import Testing

@testable import Lionheart

/// Last time on a movement, as the entry row reads it.
@Suite("Previous column")
struct PreviousColumnTests {
    /// Last time on bench: a warm-up, then 160 × 8 and 160 × 6.
    static let lastTime = PreviousPerformance(
        movementDataId: AWorkout.bench, trainingSessionId: UUID(),
        date: CalendarDay(year: 2026, month: 10, day: 28), startedAt: nil,
        sets: [entry(95, 10, kind: .warmUp), entry(160, 8), entry(160, 6)])

    static func entry(_ weight: Double, _ reps: Int, kind: SetKind = .working) -> SetEntry {
        .lift(SetEntry.Lift(
            common: SetEntry.Common(source: .formed, facets: []), movementDataId: AWorkout.bench,
            reps: reps, weight: weight, unit: .pounds, rpe: .unrated, kind: kind))
    }

    private static func lift(_ command: SetCommand) -> SetCommand.Lift? { command.liftPart }

    @Test("The first set opens on last time's first working set")
    func theFirstSetOpensOnLastTime() {
        let next = Self.lift(SetDefaults.nextSet(on: AWorkout.bench, in: AWorkout.live(), previous: Self.lastTime))

        #expect(next?.weight == 160)
        #expect(next?.reps == 8)
        #expect(next?.unit == .pounds)
        #expect(SetDefaults.carriesWeightForward(on: AWorkout.bench, in: AWorkout.live(), previous: Self.lastTime))
    }

    @Test("The second set keeps today's weight and aims at last time's second set")
    func theSecondSetAimsAtLastTimesSecond() {
        let workout = AWorkout.live(sets: [AWorkout.lift(reps: 7, weight: 165, unit: .pounds)])

        let next = Self.lift(SetDefaults.nextSet(on: AWorkout.bench, in: workout, previous: Self.lastTime))

        #expect(next?.weight == 165)
        #expect(next?.reps == 6)
    }

    @Test("A warm-up is not a precedent: the set after a ramp opens on the work")
    func aWarmUpIsNotAPrecedent() {
        let workout = AWorkout.live(sets: [AWorkout.lift(reps: 10, weight: 45, unit: .pounds, kind: .warmUp)])

        let next = Self.lift(SetDefaults.nextSet(on: AWorkout.bench, in: workout, previous: Self.lastTime))

        #expect(next?.weight == 160)
        #expect(next?.reps == 8)
        #expect(next?.kind == .working)
    }

    @Test("A plan's target beats last time: it was written knowing last time")
    func aTargetBeatsLastTime() {
        let workout = AWorkout.live(prescriptions: [AWorkout.liftTarget(reps: 3, weight: 185)])

        let next = Self.lift(SetDefaults.nextSet(on: AWorkout.bench, in: workout, previous: Self.lastTime))

        #expect(next?.weight == 185)
        #expect(next?.reps == 3)
    }

    @Test("Past last time's depth, the last set logged carries on unchanged")
    func pastLastTimesDepthTheLastSetCarries() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(reps: 8, weight: 160, unit: .pounds),
            AWorkout.lift(reps: 6, weight: 160, unit: .pounds),
        ])

        let next = Self.lift(SetDefaults.nextSet(on: AWorkout.bench, in: workout, previous: Self.lastTime))

        #expect(next?.reps == 6)
    }

    @Test("The entry row always says last time, and where the fields stand against it")
    func theGuideSaysLastTimeAndTheStanding() {
        let workout = AWorkout.live()
        var draft = SetDraft(next: AWorkout.bench, in: workout, previous: Self.lastTime)

        let opened = SetGuide.of(draft, in: workout, previous: Self.lastTime, movement: nil, bodyweight: BodyweightSeries([]))
        draft.weight = "170"
        let changed = SetGuide.of(draft, in: workout, previous: Self.lastTime, movement: nil, bodyweight: BodyweightSeries([]))

        #expect(opened.lastTime?.set == Self.entry(160, 8))
        #expect(opened.lastTime?.versus == .same)
        #expect(changed.lastTime?.versus == .load(10))
    }

    @Test("Standing against last time names the one figure that moved, or the estimated single when both did")
    func standingAgainstLastTime() {
        var draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 9, weight: 160, unit: .pounds, rpe: .unrated)))
        #expect(SetGuide.versus(draft, against: Self.entry(160, 8)) == .reps(1))

        // 165 × 7 against 160 × 8: 203.5 against 202.67 by Epley.
        draft.weight = "165"
        draft.reps = "7"
        guard case .estimate(let change) = SetGuide.versus(draft, against: Self.entry(160, 8)) else {
            Issue.record("Expected an estimated-single comparison")
            return
        }
        #expect(abs(change - 0.8333) < 0.001)
        #expect(SetGuide.Versus.estimate(change).isAhead)

        draft.weightUnit = .kilograms
        #expect(SetGuide.versus(draft, against: Self.entry(160, 8)) == .incomparable)
        draft.weightUnit = .pounds
        draft.reps = ""
        #expect(SetGuide.versus(draft, against: Self.entry(160, 8)) == .incomparable)
    }

    @Test("A warm-up being typed is compared with nothing")
    func aWarmUpHasNoLastTime() {
        var draft = SetDraft(next: AWorkout.bench, in: AWorkout.live(), previous: Self.lastTime)
        draft.kind = .warmUp
        draft.weight = "95"

        let guide = SetGuide.of(
            draft, in: AWorkout.live(), previous: Self.lastTime, movement: nil, bodyweight: BodyweightSeries([]))

        #expect(guide.lastTime == nil)
    }
}

/// Plates on a bar, and the ramp to a working weight.
@Suite("Plates and warm-ups")
struct PlatesAndWarmUpTests {
    @Test("A weight is loaded heaviest plate first, per side", arguments: [
        (225.0, [45.0, 45.0]),
        (195.0, [45.0, 25.0, 5.0]),
        (135.0, [45.0]),
        (90.0, [10.0, 10.0, 2.5]),
    ])
    func plates(total: Double, perSide: [Double]) {
        #expect(PlateLoading.of(total) == .loaded(perSide: perSide, short: 0))
        #expect(PlateLoading.of(total).total() == total)
    }

    @Test("The bar alone, or less, loads nothing")
    func theBarAloneLoadsNothing() {
        #expect(PlateLoading.of(45) == .barOnly)
        #expect(PlateLoading.of(30) == .barOnly)
    }

    @Test("A weight the plates cannot make is loaded under it, and the gap is said")
    func anUnloadableWeightSaysTheGap() {
        #expect(PlateLoading.of(227) == .loaded(perSide: [45, 45], short: 2))
    }

    @Test("A kilogram set gets no plate set: the gym's kilogram plates are not known")
    func kilogramsHaveNoPlates() {
        #expect(PlateSet.for(.kilograms) == nil)
        #expect(PlateSet.for(.pounds) == .pounds)
    }

    @Test("A barbell ramp starts on the bar and rounds each step down to plate pairs")
    func aBarbellRamp() {
        let ramp = WarmUpRamp.to(225, unit: .pounds, loading: .barbell(.pounds))

        #expect(ramp.steps.map(\.weight) == [45, 110, 155, 190])
        #expect(ramp.steps.map(\.reps) == [10, 5, 3, 1])
    }

    @Test("A weighted dip warms up at bodyweight, then with some of the plate")
    func aWeightedDipRamp() {
        let ramp = WarmUpRamp.to(45, unit: .pounds, loading: .addedToBodyweight)

        #expect(ramp.steps == [.init(weight: 0, reps: 8), .init(weight: 25, reps: 3)])
    }

    @Test("A step that rounds onto the one before it, or onto the work, is dropped")
    func stepsStayStrictlyRisingAndUnderTheWork() {
        let ramp = WarmUpRamp.to(60, unit: .pounds, loading: .barbell(.pounds))

        let weights = ramp.steps.map(\.weight)
        #expect(weights == weights.sorted())
        #expect(Set(weights).count == weights.count)
        #expect(weights.allSatisfy { $0 < 60 })
    }

    @Test("A working weight no heavier than the bar needs no ramp")
    func aLightWorkingWeightNeedsNoRamp() {
        #expect(WarmUpRamp.to(45, unit: .pounds, loading: .barbell(.pounds)).isEmpty)
        #expect(WarmUpRamp.to(0, unit: .pounds, loading: .other).isEmpty)
    }
}

/// What a warm-up does and does not count toward.
@Suite("Set kinds in the workout")
struct SetKindRuleTests {
    @Test("Warm-ups count toward no total")
    func warmUpsCountTowardNothing() {
        let totals = WorkoutTotals.of([
            AWorkout.lift(reps: 10, weight: 45, unit: .pounds, kind: .warmUp),
            AWorkout.lift(reps: 5, weight: 185, unit: .pounds),
            AWorkout.lift(reps: 8, weight: 135, unit: .pounds, kind: .drop),
        ])

        #expect(totals.setCount == 2)
        #expect(totals.weights.first?.volume == 2005)  // 185 × 5 + 135 × 8
    }

    @Test("Warm-ups are numbered apart, answer no target and do not advance the plan")
    func warmUpsAreNumberedApart() {
        let workout = AWorkout.live(
            sets: [
                AWorkout.lift(reps: 10, weight: 45, kind: .warmUp),
                AWorkout.lift(reps: 5, weight: 100),
            ],
            prescriptions: [AWorkout.liftTarget(), AWorkout.liftTarget()])

        let movement = WorkoutReading.of(workout).movements[0]

        #expect(movement.sets.map(\.number) == [1, 1])
        #expect(movement.sets.map(\.isWarmUp) == [true, false])
        #expect(movement.sets[0].target == nil)
        #expect(movement.sets[1].target == AWorkout.liftTarget())
        #expect(movement.progress == .within(logged: 1, of: 2))
    }

    @Test("A routine's set count is the plan where nothing was prescribed")
    func aRoutineCountIsThePlan() {
        let workout = ActiveWorkout(
            destination: .liveSession(AWorkout.session), startedAt: AWorkout.start,
            sets: [AWorkout.lift(), AWorkout.lift(), AWorkout.lift()],
            addedMovementDataIds: [AWorkout.bench, AWorkout.squat],
            routine: WorkoutRoutine(routineId: UUID(), name: "Upper", movements: [
                RoutineMovement(movementDataId: AWorkout.bench, workingSets: 2),
                RoutineMovement(movementDataId: AWorkout.squat, workingSets: 3),
            ]))

        let reading = WorkoutReading.of(workout)

        #expect(reading.movements.map(\.movementDataId) == [AWorkout.bench, AWorkout.squat])
        #expect(reading.movements[0].progress == .beyond(planned: 2, extra: 1))
        #expect(reading.movements[1].progress == .within(logged: 0, of: 3))
        #expect(reading.focus(given: nil) == AWorkout.squat)
    }

    @Test("A warm-up never says it beat a record, and never raises the bar for the work")
    func aWarmUpNeverBeatsARecord() {
        let warmUp = SetCommand.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 1, weight: 500, unit: .kilograms, rpe: .unrated, kind: .warmUp))
        let work = AWorkout.lift(reps: 5, weight: 101).command

        #expect(SetRecordOutcome.of(warmUp, after: [], against: nil) == .notApplicable)
        #expect(SetRecordOutcome.of(work, after: [warmUp], against: nil)
            == SetRecordOutcome.of(work, after: [], against: nil))
    }
}

/// The athlete's bodyweight as other screens ask about it.
@Suite("Bodyweight")
struct BodyweightRuleTests {
    static let series = BodyweightSeries([
        BodyweightMeasurement(day: CalendarDay(year: 2026, month: 10, day: 20), weight: 175, unit: .pounds),
        BodyweightMeasurement(day: CalendarDay(year: 2026, month: 10, day: 6), weight: 176.4, unit: .pounds),
        BodyweightMeasurement(day: CalendarDay(year: 2026, month: 11, day: 2), weight: 175.2, unit: .pounds),
    ])

    @Test("The reading in force on a day is the newest on or before it")
    func theReadingInForce() {
        #expect(Self.series.inForce(on: CalendarDay(year: 2026, month: 10, day: 25))?.weight == 175)
        #expect(Self.series.inForce(on: CalendarDay(year: 2026, month: 10, day: 6))?.weight == 176.4)
        #expect(Self.series.inForce(on: CalendarDay(year: 2026, month: 10, day: 5)) == nil)
        #expect(Self.series.latest?.weight == 175.2)
    }

    @Test("The change over a span is from the reading in force at its start")
    func theChangeOverASpan() {
        let change = Self.series.change(over: 20)
        #expect(change.map { ($0 * 10).rounded() / 10 } == -1.2)
        #expect(Self.series.change(over: 60) == nil)
    }

    @Test("A reading logged in kilograms is charted and compared in pounds")
    func kilogramsReadInPounds() {
        let mixed = BodyweightSeries(Self.series.measurements + [
            BodyweightMeasurement(day: CalendarDay(year: 2026, month: 11, day: 3), weight: 79.5, unit: .kilograms),
        ])

        let points = mixed.pointsInPounds()
        #expect(points.count == 4)
        // 79.5 kg is 175.3 lb, so a scale switched to kilograms reads as the same man, not a drop.
        #expect(points.last?.value == 175.3)
        #expect(mixed.change(over: 1).map { ($0 * 10).rounded() / 10 } == 0.1)
    }

    @Test("System load is bodyweight plus the plate, only where the setup says so and the units agree")
    func systemLoad() {
        let dip = MovementSetup(notes: "", addsToBodyweight: true)
        let weighed = Self.series.latest

        #expect(SystemLoad.of(weight: 45, unit: .pounds, setup: dip, bodyweight: weighed)
            .map { ($0.total * 10).rounded() } == 2202)
        #expect(SystemLoad.of(weight: 45, unit: .pounds, setup: .none, bodyweight: weighed) == nil)
        #expect(SystemLoad.of(weight: 20, unit: .kilograms, setup: dip, bodyweight: weighed) == nil)
        #expect(SystemLoad.of(weight: 45, unit: .pounds, setup: dip, bodyweight: nil) == nil)
    }
}

/// The readout of a finished workout.
@Suite("Workout summary")
struct WorkoutSummaryTests {
    static let dip = UUID(uuidString: "55555555-5555-5555-5555-555555555555")!
    static let dipSetup = [dip: MovementSetup(notes: "", addsToBodyweight: true)]

    static func dipSet(_ weight: Double, _ reps: Int) -> LoggedSet {
        AWorkout.lift(dip, reps: reps, weight: weight, unit: .pounds)
    }

    static func lastDips(on day: CalendarDay, _ sets: [(Double, Int)]) -> [UUID: PreviousPerformance] {
        [dip: PreviousPerformance(
            movementDataId: dip, trainingSessionId: UUID(), date: day, startedAt: nil,
            sets: sets.map { weight, reps in
                .lift(SetEntry.Lift(
                    common: SetEntry.Common(source: .formed, facets: []), movementDataId: dip,
                    reps: reps, weight: weight, unit: .pounds, rpe: .unrated))
            })]
    }

    private static func summary(
        _ workout: ActiveWorkout,
        previous: [UUID: PreviousPerformance] = [:],
        bodyweight: BodyweightSeries = BodyweightSeries([]),
        last: TrainingSession.Completed? = nil
    ) -> WorkoutSummary {
        WorkoutSummary.of(
            workout, endingAt: AWorkout.start.addingTimeInterval(3000), previous: previous, records: [:],
            setups: dipSetup, bodyweight: bodyweight, lastRoutineSession: last)
    }

    @Test("A weighted dip is compared on bodyweight plus plate when both days were weighed")
    func dipsCompareOnSystemLoad() {
        let weighed = BodyweightSeries([
            BodyweightMeasurement(day: CalendarDay(year: 2026, month: 2, day: 1), weight: 175, unit: .pounds),
        ])
        let workout = AWorkout.live(sets: [Self.dipSet(50, 6)])

        let line = Self.summary(
            workout, previous: Self.lastDips(on: CalendarDay(year: 2026, month: 2, day: 20), [(45, 6)]),
            bodyweight: weighed
        ).movements[0]

        #expect(line.best?.load == 225)
        #expect(line.previousBest?.load == 220)
        #expect(line.change.map { ($0 * 1000).rounded() } == 23)
    }

    @Test("When last time was not weighed, both days are compared on the plate alone")
    func dipsCompareOnPlateWithoutAnOldBodyweight() {
        let weighedOnlyToday = BodyweightSeries([
            BodyweightMeasurement(day: CalendarDay(AWorkout.start), weight: 175, unit: .pounds),
        ])
        let workout = AWorkout.live(sets: [Self.dipSet(50, 6)])

        let line = Self.summary(
            workout, previous: Self.lastDips(on: CalendarDay(year: 2025, month: 9, day: 16), [(70, 3)]),
            bodyweight: weighedOnlyToday
        ).movements[0]

        #expect(line.best?.load == 50)
        #expect(line.previousBest?.load == 70)
    }

    @Test("Updating a routine keeps a movement skipped today, and drops one added and never trained")
    func updatingARoutineKeepsWhatWasSkipped() {
        let routine = WorkoutRoutine(routineId: UUID(), name: "Upper", movements: [
            RoutineMovement(movementDataId: Self.dip, workingSets: 2),
            RoutineMovement(movementDataId: AWorkout.bench, workingSets: 2),
        ])
        let workout = ActiveWorkout(
            destination: .liveSession(AWorkout.session), startedAt: AWorkout.start,
            sets: [Self.dipSet(50, 6), Self.dipSet(50, 5), Self.dipSet(50, 4), AWorkout.lift(AWorkout.squat)],
            addedMovementDataIds: [Self.dip, AWorkout.bench, AWorkout.squat, AWorkout.treadmill],
            routine: routine)

        let summary = Self.summary(workout)

        #expect(summary.routineMovements == [
            RoutineMovement(movementDataId: Self.dip, workingSets: 3),
            RoutineMovement(movementDataId: AWorkout.bench, workingSets: 2),
            RoutineMovement(movementDataId: AWorkout.squat, workingSets: 1),
        ])
        #expect(summary.departsFromRoutine)
    }

    @Test("A drop set extends the set before it rather than becoming one of the routine's sets")
    func aDropSetIsNotARoutineSet() {
        let routine = WorkoutRoutine(routineId: UUID(), name: "Upper", movements: [
            RoutineMovement(movementDataId: Self.dip, workingSets: 2),
        ])
        let workout = ActiveWorkout(
            destination: .liveSession(AWorkout.session), startedAt: AWorkout.start,
            sets: [Self.dipSet(50, 6), Self.dipSet(50, 5),
                   AWorkout.lift(Self.dip, reps: 8, weight: 25, unit: .pounds, kind: .drop)],
            addedMovementDataIds: [Self.dip], routine: routine)

        #expect(!Self.summary(workout).departsFromRoutine)
        #expect(Self.summary(workout).movements.first?.workingSets == 2)
        #expect(WorkoutReading.of(workout).movements.first?.progress == .within(logged: 2, of: 2))
    }

    @Test("A drop set takes no place, so the next set is compared with last time's next working set")
    func aDropSetTakesNoPlace() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(reps: 8, weight: 160, unit: .pounds),
            AWorkout.lift(reps: 10, weight: 115, unit: .pounds, kind: .drop),
        ])

        let next = SetDefaults.nextSet(on: AWorkout.bench, in: workout, previous: PreviousColumnTests.lastTime).liftPart

        #expect(workout.workingSetCount(on: AWorkout.bench) == 1)
        #expect(next?.weight == 160)
        #expect(next?.reps == 6)
    }

    @Test("A workout trained exactly as its routine offers no update")
    func aFaithfulWorkoutOffersNoUpdate() {
        let routine = WorkoutRoutine(routineId: UUID(), name: "Upper", movements: [
            RoutineMovement(movementDataId: Self.dip, workingSets: 2),
        ])
        let workout = ActiveWorkout(
            destination: .liveSession(AWorkout.session), startedAt: AWorkout.start,
            sets: [AWorkout.lift(Self.dip, reps: 10, weight: 0, unit: .pounds, kind: .warmUp),
                   Self.dipSet(50, 6), Self.dipSet(50, 5)],
            addedMovementDataIds: [Self.dip], routine: routine)

        #expect(!Self.summary(workout).departsFromRoutine)
    }

    @Test("The last session of the routine is read as the same figures, warm-ups left out")
    func theRoutinesLastSessionIsTheComparison() throws {
        let last = try #require(try Decoding.decode(TrainingSession.self, Session.completed(
            sets: """
                [{"$type":"lift","movementDataId":"\(Session.movementId)","reps":10,"weight":45,"unit":1,"rpe":null,"kind":1,"source":{"$type":"formed"},"facets":[]},
                 {"$type":"lift","movementDataId":"\(Session.movementId)","reps":5,"weight":185,"unit":1,"rpe":null,"source":{"$type":"formed"},"facets":[]}]
                """,
            period: #"{"$type":"interval","start":"2026-03-04T18:00:00+00:00","end":"2026-03-04T19:00:00+00:00"}"#
        )).completedSession)

        let comparison = try #require(Self.summary(AWorkout.live(sets: [Self.dipSet(50, 6)]), last: last).comparison)

        #expect(comparison.duration == 3600)
        #expect(comparison.totals.setCount == 1)
        #expect(comparison.totals.weights.first?.volume == 925)
    }
}

/// A finished session read back in History and on a movement's page, warm-ups and all.
@Suite("Set kinds in the log")
struct SetKindLogTests {
    private static func entry(_ weight: Double, _ reps: Int, kind: SetKind = .working) -> SetEntry {
        PreviousColumnTests.entry(weight, reps, kind: kind)
    }

    @Test("A finished session's totals leave its warm-ups out, as the workout did")
    func aSessionsTotalsLeaveWarmUpsOut() {
        let totals = WorkoutTotals.of(entries: [Self.entry(135, 10, kind: .warmUp), Self.entry(225, 5)])

        #expect(totals.setCount == 1)
        #expect(totals.weights.first?.volume == 1125)
    }

    @Test("A ramp before straight sets still reads as straight sets at the working load")
    func aRampBeforeStraightSetsReadsStraight() {
        let line = MovementDigest.line(of: [
            Self.entry(45, 10, kind: .warmUp), Self.entry(135, 5, kind: .warmUp),
            Self.entry(225, 8), Self.entry(225, 6),
        ])

        #expect(line == .straight(weight: 225, unit: .pounds, reps: [8, 6]))
    }

    @Test("Sets are numbered within their kind, so the first working set is set 1")
    func setsAreNumberedWithinTheirKind() {
        #expect(SetKind.numbers(of: [.warmUp, .warmUp, .working, .drop, .working]) == [1, 2, 1, 2, 3])
    }
}

/// What finished workouts landing from the outbox set off.
@MainActor
@Suite("Outbox landing")
struct OutboxLandingTests {
    private static func aFinishedWorkout() -> FinishedWorkout {
        FinishedWorkout(
            workout: ActiveWorkout(destination: .afterTheFact, startedAt: AWorkout.start, sets: [AWorkout.words()]),
            endedAt: AWorkout.start.addingTimeInterval(3000), effort: nil)
    }

    @Test("A workout landing from the outbox has last time read again")
    func aLandingRunsTheHandlers() async {
        let (outbox, _) = anOutbox(sending: ARecordingServer())
        var landings = 0
        outbox.onLanded { landings += 1 }
        _ = outbox.hold(Self.aFinishedWorkout())

        await outbox.deliver()

        #expect(landings == 1)
        #expect(outbox.finished.isEmpty)
    }

    @Test("A run the server was not there for sets nothing off")
    func aRunThatLandsNothingRunsNoHandler() async {
        let server = ARecordingServer()
        server.failNextLogs = [APIError.transport("The network is not reachable.")]
        let (outbox, _) = anOutbox(sending: server)
        var landings = 0
        outbox.onLanded { landings += 1 }
        _ = outbox.hold(Self.aFinishedWorkout())

        await outbox.deliver()

        #expect(landings == 0)
        #expect(outbox.finished.count == 1)
    }
}
