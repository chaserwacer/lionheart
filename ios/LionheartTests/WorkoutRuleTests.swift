import Foundation
import Testing

@testable import Lionheart

/// What the workout adds up to.
@Suite("Workout totals")
struct WorkoutTotalsTests {
    @Test("Volume is weight times reps, summed — the server's own arithmetic")
    func volumeIsWeightTimesReps() {
        let totals = WorkoutTotals.of([
            AWorkout.lift(reps: 5, weight: 100),
            AWorkout.lift(reps: 3, weight: 110),
        ])

        #expect(totals.weights.count == 1)
        #expect(totals.weights[0].volume == 830)
        #expect(totals.weights[0].reps == 8)
        #expect(totals.weights[0].setCount == 2)
        #expect(totals.setCount == 2)
    }

    @Test("Mixed units are totalled apart rather than converted")
    func mixedUnitsAreKeptApart() {
        // The server compares a record's weight numerically without converting, so a device-side
        // conversion would print a volume that disagrees with the record beside it.
        let totals = WorkoutTotals.of([
            AWorkout.lift(reps: 5, weight: 100, unit: .kilograms),
            AWorkout.lift(reps: 5, weight: 100, unit: .pounds),
        ])

        #expect(totals.weights.map(\.unit) == [.kilograms, .pounds])
        #expect(totals.weights.allSatisfy { $0.volume == 500 })
    }

    @Test("Units are ordered by the one used first")
    func unitsAreInFirstAppearanceOrder() {
        let totals = WorkoutTotals.of([
            AWorkout.lift(weight: 100, unit: .pounds),
            AWorkout.lift(weight: 100, unit: .kilograms),
        ])

        #expect(totals.weights.map(\.unit) == [.pounds, .kilograms])
    }

    @Test("Distance work and trail runs both count towards distance and moving time")
    func bothDistanceShapesCount() {
        let totals = WorkoutTotals.of([
            AWorkout.run(distance: 5, duration: 1470),
            AWorkout.trailRun(distance: 10),
        ])

        #expect(totals.distances.count == 1)
        #expect(totals.distances[0].distance == 15)
        #expect(totals.movingDuration == 1470 + 3900)
    }

    @Test("A set logged as words contributes nothing but its count")
    func wordsAddNoFigures() {
        let totals = WorkoutTotals.of([AWorkout.words()])

        #expect(totals.setCount == 1)
        #expect(totals.weights.isEmpty)
        #expect(totals.distances.isEmpty)
        #expect(totals.movingDuration == 0)
    }

    @Test("No sets is no totals")
    func nothingLoggedIsEmpty() {
        #expect(WorkoutTotals.of([]).isEmpty)
    }
}

/// How the surface reads the workout.
@Suite("Workout reading")
struct WorkoutReadingTests {
    @Test("Sets are grouped by movement and numbered within it")
    func setsAreNumberedWithinTheirMovement() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(AWorkout.bench, at: 0),
            AWorkout.lift(AWorkout.squat, at: 60),
            AWorkout.lift(AWorkout.bench, at: 120),
        ])

        let reading = WorkoutReading.of(workout)

        #expect(reading.movements.map(\.movementDataId) == [AWorkout.bench, AWorkout.squat])
        // Three sets in the workout, but the second bench set is bench's number two — a set on one
        // movement must not advance the numbering of another.
        #expect(reading.movements[0].sets.map(\.number) == [1, 2])
        #expect(reading.movements[1].sets.map(\.number) == [1])
    }

    @Test("A movement appears as soon as it is prescribed, before anything is logged")
    func aPrescribedMovementAppearsFirst() {
        // Opening a planned session should show the plan, not an empty screen.
        let workout = AWorkout.live(prescriptions: [
            AWorkout.liftTarget(AWorkout.squat),
            AWorkout.liftTarget(AWorkout.bench),
        ])

        let reading = WorkoutReading.of(workout)

        #expect(reading.movements.map(\.movementDataId) == [AWorkout.squat, AWorkout.bench])
        #expect(reading.movements.allSatisfy { $0.sets.isEmpty })
    }

    @Test("Prescribed movements come before ones the athlete added")
    func prescribedOrderLeads() {
        let workout = AWorkout.live(
            sets: [AWorkout.lift(AWorkout.squat)],
            prescriptions: [AWorkout.liftTarget(AWorkout.bench)])

        #expect(WorkoutReading.of(workout).movements.map(\.movementDataId)
            == [AWorkout.bench, AWorkout.squat])
    }

    @Test("Each set is matched to the target at its own position in the movement")
    func setsAnswerTargetsInOrder() {
        let workout = AWorkout.live(
            sets: [AWorkout.lift(reps: 5, weight: 100), AWorkout.lift(reps: 5, weight: 102.5)],
            prescriptions: [
                AWorkout.liftTarget(reps: 5, weight: 100),
                AWorkout.liftTarget(reps: 5, weight: 105),
            ])

        let movement = WorkoutReading.of(workout).movements[0]

        #expect(movement.sets[0].target == AWorkout.liftTarget(reps: 5, weight: 100))
        #expect(movement.sets[1].target == AWorkout.liftTarget(reps: 5, weight: 105))
        #expect(movement.nextTarget == nil)
        #expect(movement.isComplete)
    }

    @Test("The next target is the first one no set has answered")
    func theNextTargetIsTheFirstUnanswered() {
        let workout = AWorkout.live(
            sets: [AWorkout.lift()],
            prescriptions: [
                AWorkout.liftTarget(weight: 100),
                AWorkout.liftTarget(weight: 105),
                AWorkout.liftTarget(weight: 110),
            ])

        let movement = WorkoutReading.of(workout).movements[0]

        #expect(movement.nextTarget == AWorkout.liftTarget(weight: 105))
        #expect(movement.remainingTargetCount == 2)
        #expect(!movement.isComplete)
    }

    @Test("An extra set beyond the plan answers no target and does not break the numbering")
    func anExtraSetHasNoTarget() {
        let workout = AWorkout.live(
            sets: [AWorkout.lift(), AWorkout.lift()],
            prescriptions: [AWorkout.liftTarget()])

        let movement = WorkoutReading.of(workout).movements[0]

        #expect(movement.sets[1].target == nil)
        #expect(movement.isComplete)
        #expect(movement.remainingTargetCount == 0)
    }

    @Test("A movement with no targets is never complete, because nothing was asked of it")
    func anUnplannedMovementIsNeverComplete() {
        let workout = AWorkout.live(sets: [AWorkout.lift()])

        #expect(!WorkoutReading.of(workout).movements[0].isComplete)
    }

    @Test("Sets with no movement are kept apart rather than forced into one")
    func unattachedSetsStandAlone() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(), AWorkout.trailRun(), AWorkout.words(),
        ])

        let reading = WorkoutReading.of(workout)

        #expect(reading.movements.count == 1)
        #expect(reading.unattached.count == 2)
    }

    @Test("A target naming no movement is kept as a loose one")
    func aFreeformTargetIsLoose() {
        let workout = AWorkout.live(prescriptions: [
            AWorkout.liftTarget(), .freeform(text: "mobility, 10 minutes"),
        ])

        let reading = WorkoutReading.of(workout)

        #expect(reading.movements.count == 1)
        #expect(reading.looseTargets == [.freeform(text: "mobility, 10 minutes")])
    }

    @Test("The focus stays on the movement being worked on while it is still in the workout")
    func focusStaysPut() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(AWorkout.bench), AWorkout.lift(AWorkout.squat),
        ])

        #expect(WorkoutReading.of(workout).focus(given: AWorkout.squat) == AWorkout.squat)
    }

    @Test("With nothing being worked on, the focus is the first movement with a target left")
    func focusFallsToTheFirstUnfinishedMovement() {
        // Starting a planned session should put the first prescribed movement under the thumb.
        let workout = AWorkout.live(
            sets: [AWorkout.lift(AWorkout.bench)],
            prescriptions: [AWorkout.liftTarget(AWorkout.bench), AWorkout.liftTarget(AWorkout.squat)])

        #expect(WorkoutReading.of(workout).focus(given: nil) == AWorkout.squat)
    }

    @Test("A movement that has left the workout does not keep the focus")
    func focusDoesNotPointAtNothing() {
        let workout = AWorkout.live(sets: [AWorkout.lift(AWorkout.bench)])

        #expect(WorkoutReading.of(workout).focus(given: AWorkout.treadmill) == AWorkout.bench)
    }

    @Test("An empty workout has nothing to focus on")
    func anEmptyWorkoutHasNoFocus() {
        #expect(WorkoutReading.of(AWorkout.live()).focus(given: nil) == nil)
    }
}

/// The rule that decides whether logging is fast.
@Suite("Set defaults")
struct SetDefaultsTests {
    @Test("The last set on this movement is what the next one starts from")
    func theLastSetIsCarriedForward() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(reps: 5, weight: 100, rpe: .rated(7)),
            AWorkout.lift(reps: 3, weight: 110, rpe: .rated(9)),
        ])

        let next = SetDefaults.nextSet(on: AWorkout.bench, in: workout)

        #expect(next.liftPart?.reps == 3)
        #expect(next.liftPart?.weight == 110)
        #expect(next.rpe == .rated(9))
    }

    @Test("The last set beats the plan, because a plan is an intention and a set is a fact")
    func theLastSetBeatsTheTarget() {
        // An athlete who dropped the weight on set three does not want set four pre-filled with
        // the plan they abandoned.
        let workout = AWorkout.live(
            sets: [AWorkout.lift(reps: 3, weight: 90)],
            prescriptions: [AWorkout.liftTarget(reps: 5, weight: 102.5)])

        #expect(SetDefaults.nextSet(on: AWorkout.bench, in: workout).liftPart?.weight == 90)
    }

    @Test("With nothing logged, the next target is what the fields hold")
    func theTargetSeedsTheFirstSet() {
        let workout = AWorkout.live(prescriptions: [AWorkout.liftTarget(reps: 5, weight: 102.5)])

        let next = SetDefaults.nextSet(on: AWorkout.bench, in: workout)

        #expect(next.liftPart?.reps == 5)
        #expect(next.liftPart?.weight == 102.5)
        // The target's own RPE is a target, not something the athlete has felt yet.
        #expect(next.rpe == .unrated)
    }

    @Test("A movement with no history and no target starts empty, as a lift")
    func anUnknownMovementStartsEmpty() {
        let next = SetDefaults.nextSet(on: AWorkout.bench, in: AWorkout.live())

        #expect(next.liftPart?.reps == 0)
        #expect(next.liftPart?.weight == 0)
        #expect(next.modality == .lift)
    }

    @Test("Only this movement's own sets carry forward")
    func otherMovementsDoNotLeak() {
        let workout = AWorkout.live(sets: [AWorkout.lift(AWorkout.squat, reps: 5, weight: 140)])

        #expect(SetDefaults.nextSet(on: AWorkout.bench, in: workout).liftPart?.weight == 0)
    }

    @Test("The weight unit follows the athlete across movements")
    func theUnitIsCarriedAcrossMovements() {
        // The unit is a property of the athlete, not of the exercise: someone lifting in pounds
        // does so for all of them.
        let workout = AWorkout.live(sets: [AWorkout.lift(AWorkout.squat, unit: .pounds)])

        #expect(SetDefaults.nextSet(on: AWorkout.bench, in: workout).liftPart?.unit == .pounds)
    }

    @Test("A movement last logged as distance work opens as distance work")
    func theShapeIsRemembered() {
        let workout = AWorkout.live(sets: [AWorkout.run(distance: 5, duration: 1470)])

        let next = SetDefaults.nextSet(on: AWorkout.treadmill, in: workout)

        #expect(next.modality == .distanceTime)
        #expect(next.coveredPart?.distance == 5)
        #expect(next.coveredPart?.duration == 1470)
    }

    @Test("A distance target opens the distance fields even with nothing logged")
    func aDistanceTargetPicksTheShape() {
        let workout = AWorkout.live(prescriptions: [
            .distanceTime(SetPrescription.DistanceTime(
                movementDataId: AWorkout.treadmill, targetDistance: 5,
                unit: .kilometers, targetDuration: 1500)),
        ])

        #expect(SetDefaults.nextSet(on: AWorkout.treadmill, in: workout).modality == .distanceTime)
    }

    @Test("Asking for a shape overrides what was last used")
    func aForcedShapeWins() {
        let workout = AWorkout.live(sets: [AWorkout.lift()])

        #expect(SetDefaults.nextSet(on: AWorkout.bench, in: workout, as: .distanceTime).modality
            == .distanceTime)
    }

    @Test("A trail run starts from the last trail run of the workout")
    func trailRunsCarryForward() {
        let workout = AWorkout.live(sets: [AWorkout.trailRun(distance: 10, gain: 320)])

        let next = SetDefaults.nextTrailRun(in: workout)

        #expect(next.coveredPart?.distance == 10)
        #expect(next.modality == .trailRun)
    }

    @Test("The first trail run of a workout starts empty")
    func theFirstTrailRunIsEmpty() {
        #expect(SetDefaults.nextTrailRun(in: AWorkout.live()).coveredPart?.distance == 0)
    }
}

/// Whether a set beat anything.
@Suite("Set record outcome")
struct SetRecordOutcomeTests {
    private static func set(_ weight: Double, _ reps: Int, _ unit: WeightUnit = .pounds) -> SetCommand {
        .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: reps, weight: weight, unit: unit, rpe: .unrated))
    }

    /// A bench holding a pounds book — heaviest 225 × 1, a 205 five, 1,000 lb of set volume.
    private static let pounds = ARecord.held([
        ARecord.heaviest(225, 1),
        ARecord.repMax(205, 5),
        ARecord.setVolume(100, 10),
    ])

    @Test("A heavier five beats the five-rep max, and says so ahead of the volume it also beat")
    func aHeavierFiveIsARepMax() {
        #expect(SetRecordOutcome.of(Self.set(210, 5), against: Self.pounds)
            == .beats(.repMax(reps: 5), also: [.bestSetVolume]))
    }

    @Test("A heavier single beats the heaviest set and the one-rep max it has no record of stays quiet")
    func aFirstRepMaxIsNotNews() {
        // No 1RM stands in this book, so the single is the first of its kind there: the server
        // will set one, but announcing firsts would make the badge mean nothing.
        #expect(SetRecordOutcome.of(Self.set(230, 1), against: Self.pounds) == .beats(.heaviestSet, also: []))
    }

    @Test("A set in the other unit is not compared with this unit's records at all")
    func aRecordInAnotherUnitIsNotCompared() {
        // 225 lb is a bigger number than 110 kg and a lighter lift; and a kilograms set is a
        // different record, not a worse pounds one.
        let kilograms = ARecord.held([ARecord.heaviest(110, 1, .kilograms), ARecord.repMax(100, 5, .kilograms)])

        #expect(SetRecordOutcome.of(Self.set(225, 5, .pounds), against: kilograms) == .noRecordYet)
        #expect(SetRecordOutcome.of(Self.set(105, 5, .kilograms), against: kilograms)
            == .beats(.repMax(reps: 5), also: []))
    }

    @Test("With both units held, a set competes only with its own unit's records")
    func bothUnitsHeldEachCompetesInItsOwn() {
        let both = ARecord.held(Self.pounds.records + [ARecord.heaviest(100, 1, .kilograms)])

        #expect(SetRecordOutcome.of(Self.set(105, 1, .kilograms), against: both) == .beats(.heaviestSet, also: []))
        #expect(SetRecordOutcome.of(Self.set(105, 1, .pounds), against: both) == .none)
    }

    @Test("Equalling a record does not beat it")
    func equallingIsNotBeating() {
        // Strict, which is also what keeps a repeated set from announcing itself five times.
        let book = ARecord.held([ARecord.heaviest(205, 5), ARecord.repMax(205, 5), ARecord.setVolume(205, 5)])

        #expect(SetRecordOutcome.of(Self.set(205, 5), against: book) == .none)
    }

    @Test("Past twelve reps there is no rep max to beat, but the volume still counts")
    func pastTheGridOnlyVolumeCounts() {
        let thirteens = ARecord.held([ARecord.repMax(50, 12), ARecord.setVolume(50, 12)])

        #expect(SetRecordOutcome.of(Self.set(60, 13), against: thirteens) == .beats(.bestSetVolume, also: []))
    }

    @Test("A set of nothing contests nothing, the server's own rule")
    func zeroLoadRaisesNothing() {
        #expect(SetRecordOutcome.of(Self.set(0, 5), against: Self.pounds) == .noRecordYet)
    }

    @Test("A beaten or withdrawn record is not one a set can beat again")
    func onlyStandingRecordsCompete() {
        let history = ARecord.held([
            ARecord.record(.heaviestSet(ARecord.lift(300, 1)), standing: .retracted),
        ])

        #expect(SetRecordOutcome.of(Self.set(250, 1), against: history) == .noRecordYet)
    }

    @Test("A movement with no record yet says so, and says it quietly")
    func noRecordYetIsSilent() {
        let outcome = SetRecordOutcome.of(Self.set(100, 5), against: nil)

        #expect(outcome == .noRecordYet)
        #expect(!outcome.isWorthSaying)
    }

    @Test("Only a lift is judged mid-workout; distances are the server's to decide")
    func otherShapesAreNotJudged() {
        #expect(SetRecordOutcome.of(AWorkout.run().command, against: nil) == .notApplicable)
        #expect(SetRecordOutcome.of(AWorkout.trailRun().command, against: nil) == .notApplicable)
        #expect(SetRecordOutcome.of(.freeform(text: "x"), against: nil) == .notApplicable)
    }

    @Test("A trail run looks up the trail-running records, a lift its movement's")
    func setsFindTheirSubject() {
        #expect(AWorkout.trailRun().command.recordSubject == .trailRunning)
        #expect(Self.set(100, 5).recordSubject == .movement(AWorkout.bench))
        #expect(SetCommand.freeform(text: "x").recordSubject == nil)
    }
}

/// Taking over a session the server already holds.
@Suite("Workout resumption")
struct WorkoutResumptionTests {
    private static func active(sets: [SetEntry]) -> TrainingSession.Active {
        TrainingSession.Active(
            envelope: TrainingSession.Envelope(
                trainingSessionId: AWorkout.session,
                notes: "",
                provenance: .user,
                trainingProgramId: nil,
                createdAt: nil,
                date: CalendarDay(year: 2026, month: 11, day: 2)),
            startedAt: AWorkout.start,
            sets: sets)
    }

    private static let common = SetEntry.Common(source: .formed, facets: [])

    @Test("Every shape the server holds comes back as a command that can be pushed again")
    func allShapesRestate() throws {
        let session = Self.active(sets: [
            .lift(SetEntry.Lift(
                common: Self.common, movementDataId: AWorkout.bench, reps: 5,
                weight: 100, unit: .kilograms, rpe: .rated(8))),
            .distanceTime(SetEntry.DistanceTime(
                common: Self.common, movementDataId: AWorkout.treadmill, distance: 5,
                unit: .kilometers, duration: 1470, rpe: .unrated)),
            .trailRun(SetEntry.TrailRun(
                common: Self.common, distance: 10, unit: .kilometers,
                duration: 3900, elevationGainMeters: 320, rpe: .unrated)),
            .freeform(SetEntry.Common(
                source: .texted(rawText: "some mobility", structureConfidence: 0), facets: [])),
        ])

        let restated = try WorkoutResumption.sets(of: session, loggedAt: AWorkout.start)

        #expect(restated.map(\.modality) == [.lift, .distanceTime, .trailRun, .freeform])
        #expect(restated[0].command.liftPart?.rpe == .rated(8))
        #expect(restated[3].command == .freeform(text: "some mobility"))
    }

    @Test("Every resumed set is stamped with the session's start, the only time the device knows")
    func resumedSetsTakeTheSessionStart() throws {
        let session = Self.active(sets: [.freeform(Self.common)])

        let restated = try WorkoutResumption.sets(of: session, loggedAt: AWorkout.start)

        #expect(restated.allSatisfy { $0.loggedAt == AWorkout.start })
    }

    @Test("A set shape this build cannot write back is refused rather than dropped")
    func anUnrestatableSetRefusesTheWholeSession() {
        // `PUT /sets` replaces the whole list, so resuming lossily would mean the next set logged
        // deleted the athlete's work. Refusing is what keeps it.
        let session = Self.active(sets: [
            .lift(SetEntry.Lift(
                common: Self.common, movementDataId: AWorkout.bench, reps: 5,
                weight: 100, unit: .kilograms, rpe: .unrated)),
            .unrecognised(type: "isometricHold", common: Self.common),
        ])

        #expect(throws: WorkoutResumption.Refusal.cannotRestate(type: "isometricHold")) {
            try WorkoutResumption.sets(of: session, loggedAt: AWorkout.start)
        }
    }

    @Test("A session with no sets resumes as an empty workout")
    func anEmptySessionResumes() throws {
        #expect(try WorkoutResumption.sets(of: Self.active(sets: []), loggedAt: AWorkout.start)
            .isEmpty)
    }
}

/// The countdown between sets.
@Suite("Rest clock")
struct RestClockTests {
    private static let started = Date(timeIntervalSince1970: 1000)

    @Test("A running clock counts down")
    func aRunningClockCountsDown() {
        let rest = RestClock.running(since: Self.started, duration: 120)

        #expect(rest.remaining(at: Self.started) == 120)
        #expect(rest.remaining(at: Self.started.addingTimeInterval(45)) == 75)
    }

    @Test("A clock that has run out reads as zero, never as negative")
    func anExpiredClockReadsZero() {
        let rest = RestClock.running(since: Self.started, duration: 120)

        #expect(rest.remaining(at: Self.started.addingTimeInterval(600)) == 0)
        #expect(!rest.isResting(at: Self.started.addingTimeInterval(600)))
    }

    @Test("An idle clock is not resting, which is not the same as one that has expired")
    func idleIsNotResting() {
        #expect(!RestClock.idle.isResting(at: Self.started))
        #expect(RestClock.idle.remaining(at: Self.started) == 0)
        #expect(RestClock.idle.duration == nil)
    }

    @Test("The fraction elapsed runs from none to all")
    func theFractionRunsFromNoneToAll() {
        let rest = RestClock.running(since: Self.started, duration: 100)

        #expect(rest.fractionElapsed(at: Self.started) == 0)
        #expect(rest.fractionElapsed(at: Self.started.addingTimeInterval(25)) == 0.25)
        #expect(rest.fractionElapsed(at: Self.started.addingTimeInterval(1000)) == 1)
    }

    @Test("A rest of no length reads as finished rather than dividing by nothing")
    func aZeroLengthRestIsFinished() {
        #expect(RestClock.running(since: Self.started, duration: 0).fractionElapsed(at: Self.started)
            == 1)
    }

    @Test("Extending lengthens the rest without moving its start")
    func extendingKeepsTheStart() {
        let rest = RestClock.running(since: Self.started, duration: 120)

        let longer = rest.extended(by: 30)

        // Thirty seconds more, not a fresh two minutes: the athlete has already rested ninety.
        #expect(longer == .running(since: Self.started, duration: 150))
        #expect(longer.remaining(at: Self.started.addingTimeInterval(90)) == 60)
    }

    @Test("An idle clock has nothing to extend")
    func extendingIdleDoesNothing() {
        #expect(RestClock.idle.extended(by: 30) == .idle)
    }
}

/// What the workout bar interrupts for.
@Suite("Workout attention")
struct WorkoutAttentionTests {
    @Test("A workout the device has not written down is worth saying so, whatever the server thinks")
    func unsavedToDiskNeedsAttention() {
        // The disk and the server fail independently: these sets are synced and would still be
        // lost by a force-quit, which is the case this rule exists to catch.
        #expect(WorkoutAttention.isNeeded(for: AWorkout.live(sync: .synced), savedToDisk: false))
    }

    @Test("A refused push needs the athlete, because the next one will not fix it")
    func refusedNeedsAttention() {
        #expect(WorkoutAttention.isNeeded(
            for: AWorkout.live(sync: .refused("the session is already complete")),
            savedToDisk: true))
    }

    @Test("A failed push needs the athlete too — the server does not have the work")
    func failedNeedsAttention() {
        #expect(WorkoutAttention.isNeeded(
            for: AWorkout.live(sync: .failed("offline")), savedToDisk: true))
    }

    @Test("A workout that is merely in progress is not a warning")
    func syncedAndPendingAreQuiet() {
        // Almost every set is pending for a second. Warning about that would teach the athlete to
        // stop reading the line that also carries the two failures above.
        #expect(!WorkoutAttention.isNeeded(for: AWorkout.live(sync: .synced), savedToDisk: true))
        #expect(!WorkoutAttention.isNeeded(for: AWorkout.live(sync: .pending), savedToDisk: true))
    }

    @Test("Resting is not attention")
    func restingIsQuiet() {
        let resting = AWorkout.live(rest: .running(since: AWorkout.start, duration: 120))

        #expect(!WorkoutAttention.isNeeded(for: resting, savedToDisk: true))
    }
}
