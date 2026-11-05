import Foundation
import Testing

@testable import Lionheart

/// Values a test needs a lot of, named for the domain state they stand for rather than their
/// contents, so a change to what a bench press weighs does not rename them.
enum AWorkout {
    static let session = UUID(uuidString: "11111111-1111-1111-1111-111111111111")!
    static let bench = UUID(uuidString: "22222222-2222-2222-2222-222222222222")!
    static let squat = UUID(uuidString: "33333333-3333-3333-3333-333333333333")!
    static let treadmill = UUID(uuidString: "44444444-4444-4444-4444-444444444444")!

    static let start = Date(timeIntervalSince1970: 1_772_645_400)

    static func live(
        sets: [LoggedSet] = [],
        prescriptions: [SetPrescription] = [],
        rest: RestClock = .idle,
        sync: WorkoutSyncState = .synced,
        notes: String = ""
    ) -> ActiveWorkout {
        ActiveWorkout(
            destination: .liveSession(session),
            startedAt: start,
            sync: sync,
            sets: sets,
            rest: rest,
            prescriptions: prescriptions,
            notes: notes)
    }

    static func retro(sets: [LoggedSet] = [], rest: RestClock = .idle) -> ActiveWorkout {
        ActiveWorkout(destination: .afterTheFact, startedAt: start, sets: sets, rest: rest)
    }

    static func lift(
        _ movement: UUID = bench,
        reps: Int = 5,
        weight: Double = 100,
        unit: WeightUnit = .kilograms,
        rpe: Rpe = .unrated,
        kind: SetKind = .working,
        at offset: TimeInterval = 0,
        id: UUID = UUID()
    ) -> LoggedSet {
        LoggedSet(
            id: id,
            loggedAt: start.addingTimeInterval(offset),
            command: .lift(SetCommand.Lift(
                movementDataId: movement, reps: reps, weight: weight, unit: unit, rpe: rpe, kind: kind)))
    }

    static func run(
        _ movement: UUID = treadmill,
        distance: Double = 5,
        unit: DistanceUnit = .kilometers,
        duration: TimeInterval = 1470,
        at offset: TimeInterval = 0
    ) -> LoggedSet {
        LoggedSet(
            loggedAt: start.addingTimeInterval(offset),
            command: .distanceTime(SetCommand.DistanceTime(
                movementDataId: movement, distance: distance, unit: unit,
                duration: duration, rpe: .unrated)))
    }

    static func trailRun(distance: Double = 10, gain: Double = 320) -> LoggedSet {
        LoggedSet(
            loggedAt: start,
            command: .trailRun(SetCommand.TrailRun(
                distance: distance, unit: .kilometers, duration: 3900,
                elevationGainMeters: gain, rpe: .unrated)))
    }

    static func words(_ text: String = "ten minutes of hip mobility") -> LoggedSet {
        LoggedSet(loggedAt: start, command: .freeform(text: text))
    }

    static func liftTarget(
        _ movement: UUID = bench, reps: Int = 5, weight: Double = 102.5, rpe: Rpe = .unrated
    ) -> SetPrescription {
        .lift(SetPrescription.Lift(
            movementDataId: movement, targetReps: reps, targetWeight: weight,
            unit: .kilograms, targetRpe: rpe))
    }
}

/// The persistence everything else rests on.
///
/// A set logged in a basement and lost to a force-quit is the one failure that would stop the owner
/// using the app, so the document's behaviour is asserted directly rather than taken on trust.
/// Every test writes into its own temporary directory, so none of them can see another's file.
@Suite("Active workout document")
struct ActiveWorkoutDocumentTests {
    private static func aDocument() -> ActiveWorkoutDocument {
        ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
    }

    @Test("A workout written is a workout read back, sets and all")
    func aWorkoutRoundTrips() throws {
        let document = Self.aDocument()
        let workout = AWorkout.live(
            sets: [AWorkout.lift(), AWorkout.run(), AWorkout.trailRun(), AWorkout.words()],
            prescriptions: [AWorkout.liftTarget(), .freeform(text: "mobility")],
            rest: .running(since: AWorkout.start, duration: 120),
            sync: .failed("no signal"),
            notes: "felt strong"
        ).adding(AWorkout.squat)

        #expect(document.write(workout))

        #expect(document.read() == workout)
    }

    @Test("No file is no workout, not a failure")
    func anAbsentFileIsAnAbsentWorkout() {
        #expect(Self.aDocument().read() == nil)
    }

    @Test("A cleared document is gone")
    func clearingRemovesTheWorkout() {
        let document = Self.aDocument()
        document.write(AWorkout.live())

        document.clear()

        #expect(document.read() == nil)
    }

    @Test("Truncated JSON reads as no workout rather than throwing")
    func aCorruptDocumentIsTreatedAsAbsent() throws {
        // There is no screen on which "your saved workout is corrupt" helps, and the app has to be
        // able to start.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"{"startedAt":123"#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        #expect(ActiveWorkoutDocument(directoryURL: directory).read() == nil)
    }

    @Test("A document from a newer build is refused rather than half-read")
    func aNewerDocumentIsRefused() throws {
        // A downgraded build that half-understood a future shape would write it back truncated,
        // which is how logged sets get lost for good.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"""
            {"version":99,"destination":{"kind":"afterTheFact"},"startedAt":1,
             "sync":{"kind":"synced"},"sets":[],"rest":{"idle":{}},"prescriptions":[],"notes":""}
            """#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        #expect(ActiveWorkoutDocument(directoryURL: directory).read() == nil)
    }

    @Test("A version 1 document is read rather than thrown away")
    func aVersionOneDocumentUpgrades() throws {
        // The foundation build wrote a bare session id and no sets. An athlete who updates mid
        // workout should find their workout, not an empty Train tab.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"""
            {"version":1,"sessionId":"11111111-1111-1111-1111-111111111111",
             "startedAt":1772645400,"isSynced":true}
            """#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        let restored = ActiveWorkoutDocument(directoryURL: directory).read()

        #expect(restored?.destination == .liveSession(AWorkout.session))
        #expect(restored?.sets.isEmpty == true)
        #expect(restored?.rest == .idle)
        // Pending rather than synced: version 1's flag is not read, and the safe reading of "we do
        // not know" is that the next push should carry everything.
        #expect(restored?.sync == .pending)
    }

    @Test("A version 2 document still restores, and a movement it had picked keeps its section")
    func aVersionTwoDocumentUpgrades() throws {
        // Version 2 recorded a pick only as the current movement. A phone that updates with a
        // workout in flight must find it, and the lift it had picked must still be there to log.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"""
            {"version":2,"destination":{"kind":"liveSession","sessionId":"11111111-1111-1111-1111-111111111111"},
             "startedAt":1772645400,"sync":{"kind":"synced"},"sets":[],"rest":{"idle":{}},
             "currentMovementDataId":"33333333-3333-3333-3333-333333333333",
             "prescriptions":[],"notes":""}
            """#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        let restored = try #require(ActiveWorkoutDocument(directoryURL: directory).read())

        #expect(restored.destination == .liveSession(AWorkout.session))
        #expect(restored.addedMovementDataIds == [AWorkout.squat])
        #expect(WorkoutReading.of(restored).movements.map(\.movementDataId) == [AWorkout.squat])
    }

    @Test("A version 2 document's current movement is not added when a set already names it")
    func aVersionTwoCurrentMovementWithSetsIsNotAdded() throws {
        // Adding it would move its section ahead of the others on the first launch after updating.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let written = AWorkout.live(sets: [AWorkout.lift(AWorkout.bench), AWorkout.lift(AWorkout.squat)])
            .working(on: AWorkout.squat)
        var json = try #require(
            try JSONSerialization.jsonObject(with: JSONEncoder().encode(written)) as? [String: Any])
        json["version"] = 2
        json["addedMovementDataIds"] = nil
        try JSONSerialization.data(withJSONObject: json)
            .write(to: directory.appending(path: "active-workout.json"))

        let restored = try #require(ActiveWorkoutDocument(directoryURL: directory).read())

        #expect(restored.addedMovementDataIds.isEmpty)
        #expect(restored.sets == written.sets)
    }

    @Test("A version 2 document's current movement is not added when only a target names it")
    func aVersionTwoPrescribedCurrentMovementIsNotAdded() throws {
        // The plan already gives it a section; adding it would make it removable, which a
        // prescribed movement is not.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let written = AWorkout.live(prescriptions: [AWorkout.liftTarget(AWorkout.squat)])
            .working(on: AWorkout.squat)
        var json = try #require(
            try JSONSerialization.jsonObject(with: JSONEncoder().encode(written)) as? [String: Any])
        json["version"] = 2
        json["addedMovementDataIds"] = nil
        try JSONSerialization.data(withJSONObject: json)
            .write(to: directory.appending(path: "active-workout.json"))

        let restored = try #require(ActiveWorkoutDocument(directoryURL: directory).read())

        #expect(restored.addedMovementDataIds.isEmpty)
        #expect(restored.currentMovementDataId == AWorkout.squat)
        #expect(!restored.canDrop(AWorkout.squat))
    }

    @Test("A version 5 document, written before ratings rode with the workout, reads unrated")
    func aVersionFiveDocumentReadsUnrated() throws {
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"""
            {"version":5,"destination":{"kind":"liveSession","sessionId":"11111111-1111-1111-1111-111111111111"},
             "startedAt":1772645400,"sync":{"kind":"synced"},"sets":[],"rest":{"idle":{}},
             "prescriptions":[],"addedMovementDataIds":[],"notes":""}
            """#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        let restored = try #require(ActiveWorkoutDocument(directoryURL: directory).read())

        #expect(restored.destination == .liveSession(AWorkout.session))
        #expect(restored.effort == .unrated)
    }

    @Test("Writing twice leaves the second workout, not both")
    func aWriteReplacesWhatWasThere() {
        let document = Self.aDocument()
        let first = AWorkout.live(sets: [AWorkout.lift(reps: 5)])
        let second = AWorkout.live(sets: [AWorkout.lift(reps: 8)])

        document.write(first)
        document.write(second)

        #expect(document.read() == second)
    }

    @Test("A document rewritten by this build carries this build's version")
    func rewritingStampsTheCurrentVersion() throws {
        let document = Self.aDocument()
        document.write(ActiveWorkout(
            destination: .afterTheFact, startedAt: AWorkout.start, version: 1))

        #expect(document.read()?.version == ActiveWorkout.currentVersion)
    }
}

/// The value itself.
@Suite("Active workout")
struct ActiveWorkoutValueTests {
    @Test("Elapsed time counts from the start")
    func elapsedCountsFromTheStart() {
        #expect(AWorkout.live().elapsed(at: AWorkout.start.addingTimeInterval(720)) == 720)
    }

    @Test("A clock that moved backwards does not make a workout count down")
    func elapsedNeverGoesNegative() {
        #expect(AWorkout.live().elapsed(at: AWorkout.start.addingTimeInterval(-100)) == 0)
    }

    @Test("Appending a set adds it, starts the rest and marks the workout unpushed")
    func appendingASetStartsTheRest() {
        let set = AWorkout.lift(at: 60)

        let after = AWorkout.live().appending(set, restingFor: 120)

        #expect(after.sets == [set])
        #expect(after.rest == .running(since: set.loggedAt, duration: 120))
        #expect(after.sync == .pending)
        #expect(after.currentMovementDataId == AWorkout.bench)
    }

    @Test("Appending with no rest asked for leaves the clock idle")
    func appendingWithoutRestLeavesTheClockIdle() {
        #expect(AWorkout.live().appending(AWorkout.lift(), restingFor: nil).rest == .idle)
    }

    @Test("A set with no movement keeps the movement already being worked on")
    func aTrailRunDoesNotChangeTheCurrentMovement() {
        let workout = AWorkout.live().appending(AWorkout.lift(), restingFor: nil)

        let after = workout.appending(AWorkout.trailRun(), restingFor: nil)

        #expect(after.currentMovementDataId == AWorkout.bench)
    }

    @Test("Restating a set keeps its identity and the moment it was logged")
    func restatingKeepsIdentity() {
        let original = AWorkout.lift(reps: 5, weight: 100, at: 60)
        let workout = AWorkout.live(sets: [original])

        let corrected = workout.restating(
            original.id,
            as: .lift(SetCommand.Lift(
                movementDataId: AWorkout.bench, reps: 5, weight: 102.5,
                unit: .kilograms, rpe: .unrated)))

        #expect(corrected.sets.count == 1)
        #expect(corrected.sets[0].id == original.id)
        #expect(corrected.sets[0].loggedAt == original.loggedAt)
        #expect(corrected.sets[0].command.liftPart?.weight == 102.5)
        #expect(corrected.sync == .pending)
    }

    @Test("Restating a set that is not there changes nothing")
    func restatingAnUnknownSetIsANoOp() {
        let workout = AWorkout.live(sets: [AWorkout.lift()])

        #expect(workout.restating(UUID(), as: .freeform(text: "x")) == workout)
    }

    @Test("Removing a set that is not there changes nothing")
    func removingAnUnknownSetIsANoOp() {
        let workout = AWorkout.live(sets: [AWorkout.lift()])

        #expect(workout.removing(UUID()) == workout)
    }

    @Test("Removing a set leaves the others in order")
    func removingKeepsTheRestInOrder() {
        let first = AWorkout.lift(reps: 5, at: 0)
        let second = AWorkout.lift(reps: 6, at: 60)
        let third = AWorkout.lift(reps: 7, at: 120)
        let workout = AWorkout.live(sets: [first, second, third])

        let after = workout.removing(second.id)

        #expect(after.sets.map(\.id) == [first.id, third.id])
    }

    @Test("Adding a movement moves the athlete onto it")
    func addingAMovementWorksOnIt() {
        let workout = AWorkout.live().adding(AWorkout.bench)

        #expect(workout.addedMovementDataIds == [AWorkout.bench])
        #expect(workout.currentMovementDataId == AWorkout.bench)
    }

    @Test("Adding a movement twice keeps its first place")
    func addingTwiceKeepsThePlace() {
        let workout = AWorkout.live().adding(AWorkout.bench).adding(AWorkout.squat).adding(AWorkout.bench)

        #expect(workout.addedMovementDataIds == [AWorkout.bench, AWorkout.squat])
        #expect(workout.currentMovementDataId == AWorkout.bench)
    }

    @Test("Dropping a pick with nothing logged takes it out, and off the athlete's hands")
    func droppingAnUntouchedPick() {
        let workout = AWorkout.live().adding(AWorkout.squat).adding(AWorkout.bench)

        let after = workout.dropping(AWorkout.bench)

        #expect(after.addedMovementDataIds == [AWorkout.squat])
        #expect(after.currentMovementDataId == nil)
    }

    @Test("Dropping a movement that is not the current one leaves the athlete where they were")
    func droppingAnotherMovementKeepsTheCurrentOne() {
        let workout = AWorkout.live().adding(AWorkout.squat).adding(AWorkout.bench)

        #expect(workout.dropping(AWorkout.squat).currentMovementDataId == AWorkout.bench)
    }

    @Test("Only a movement the athlete added, with nothing planned or logged on it, can be dropped")
    func onlyAnUntouchedPickCanBeDropped() {
        let workout = AWorkout.live(
            sets: [AWorkout.lift(AWorkout.squat)],
            prescriptions: [AWorkout.liftTarget(AWorkout.treadmill)]
        ).adding(AWorkout.bench)

        #expect(workout.canDrop(AWorkout.bench))
        #expect(!workout.canDrop(AWorkout.squat))
        #expect(!workout.canDrop(AWorkout.treadmill))
    }

    @Test("A movement with a set logged on it cannot be dropped")
    func aMovementWithASetStays() {
        // The set is the record that it was trained; an intention cannot take it back.
        let workout = AWorkout.live().adding(AWorkout.bench)
            .appending(AWorkout.lift(AWorkout.bench), restingFor: nil)

        #expect(workout.dropping(AWorkout.bench) == workout)
    }

    @Test("Clearing the current movement is possible, not just changing it")
    func theCurrentMovementCanBeCleared() {
        let workout = AWorkout.live().working(on: AWorkout.bench)

        #expect(workout.working(on: nil).currentMovementDataId == nil)
    }

    @Test("Ratings given mid-session are kept through every other change to the workout")
    func ratingsSurviveOtherChanges() {
        // `working(on:)` rebuilds the whole value rather than going through `with`, so a member it
        // forgets is silently reset; every change the surface makes between sets is run here.
        let effort = PerceivedEffortRequest(
            accumulatedFatigue: nil, difficultyRating: 4, engagementRating: 5, externalVariablesRating: nil)
        let rated = AWorkout.live().rating(effort)

        let after = rated
            .adding(AWorkout.squat)
            .appending(AWorkout.lift(AWorkout.bench), restingFor: 90)
            .working(on: nil)
            .resting(.idle)
            .noting("a note")
            .marking(.synced)
            .opened(as: AWorkout.session)

        #expect(after.effort == effort)
    }

    @Test("A workout of this build's version is readable, a later one is not")
    func versionDecidesReadability() {
        #expect(AWorkout.live().isReadable)
        #expect(!ActiveWorkout(
            destination: .afterTheFact,
            startedAt: AWorkout.start,
            version: ActiveWorkout.currentVersion + 1).isReadable)
    }

    @Test("A live workout pushes as it goes; one logged afterwards does not")
    func onlyALiveWorkoutPushes() {
        #expect(WorkoutDestination.liveSession(AWorkout.session).pushesAsItGoes)
        #expect(!WorkoutDestination.afterTheFact.pushesAsItGoes)
        #expect(WorkoutDestination.afterTheFact.sessionId == nil)
    }
}

/// The four sync states, and which of them mean anything to anyone.
@Suite("Workout sync state")
struct WorkoutSyncStateTests {
    @Test("Only a state another push could land is worth pushing")
    func refusedIsNotWorthPushing() {
        #expect(WorkoutSyncState.pending.isWorthPushing)
        #expect(WorkoutSyncState.failed("no signal").isWorthPushing)
        #expect(!WorkoutSyncState.synced.isWorthPushing)
        // The session is not active any more. Another identical request cannot change that.
        #expect(!WorkoutSyncState.refused("already completed").isWorthPushing)
    }

    @Test("Pending says nothing, because almost every set is pending for a moment")
    func pendingIsSilent() {
        #expect(!WorkoutSyncState.pending.needsSaying)
        #expect(!WorkoutSyncState.synced.needsSaying)
        #expect(WorkoutSyncState.failed("no signal").needsSaying)
        #expect(WorkoutSyncState.refused("already completed").needsSaying)
    }
}
