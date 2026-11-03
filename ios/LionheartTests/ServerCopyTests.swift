import Foundation
import Testing

@testable import Lionheart

/// Recognising, among what the server holds, a workout this device sent without hearing back.
@Suite("Server copy")
struct ServerCopyTests {
    private static let ours = UUID()
    private static let start = AWorkout.start

    @Test("An active session that began at this workout's start is this workout")
    func anActiveSessionAtTheSameStart() {
        let found = ServerCopy.among(
            [ARecordingServer.anActiveSession(id: Self.ours, startedAt: Self.start)], startedAt: Self.start)

        #expect(found == .active(Self.ours))
    }

    @Test("A completed session is recognised by where its period begins")
    func aCompletedSessionByItsPeriod() {
        let found = ServerCopy.among(
            [ARecordingServer.aCompletedSession(id: Self.ours, startedAt: Self.start)], startedAt: Self.start)

        #expect(found == .completed(Self.ours))
    }

    @Test("A session that began at another instant is somebody else's workout")
    func anotherStartIsNotThisWorkout() {
        let sessions = [
            ARecordingServer.anActiveSession(id: UUID(), startedAt: Self.start.addingTimeInterval(-5)),
            ARecordingServer.aCompletedSession(id: UUID(), startedAt: Self.start.addingTimeInterval(30)),
        ]

        #expect(ServerCopy.among(sessions, startedAt: Self.start) == .absent)
    }

    @Test("A planned or skipped session has no start to recognise")
    func plansAreNeverACopy() {
        let sessions = [
            ARecordingServer.aPlannedSession(id: UUID()),
            ARecordingServer.aSkippedSession(id: UUID()),
        ]

        #expect(ServerCopy.among(sessions, startedAt: Self.start) == .absent)
    }

    @Test("A plan still planned is waiting to be this workout's")
    func aStillPlannedPlan() {
        #expect(ServerCopy.ofPlan(ARecordingServer.aPlannedSession(id: Self.ours), startedAt: Self.start)
            == .planWaiting(Self.ours))
    }

    @Test("A plan this workout started or finished answers with that")
    func aPlanCarryingThisWorkout() {
        #expect(ServerCopy.ofPlan(
            ARecordingServer.anActiveSession(id: Self.ours, startedAt: Self.start), startedAt: Self.start)
            == .active(Self.ours))
        #expect(ServerCopy.ofPlan(
            ARecordingServer.aCompletedSession(id: Self.ours, startedAt: Self.start), startedAt: Self.start)
            == .completed(Self.ours))
    }

    @Test("A plan skipped, started elsewhere or removed is no longer this workout's")
    func aLostPlan() {
        #expect(ServerCopy.ofPlan(ARecordingServer.aSkippedSession(id: Self.ours), startedAt: Self.start) == nil)
        #expect(ServerCopy.ofPlan(
            ARecordingServer.anActiveSession(id: Self.ours, startedAt: Self.start.addingTimeInterval(-900)),
            startedAt: Self.start) == nil)
        #expect(ServerCopy.ofPlan(nil, startedAt: Self.start) == nil)
    }
}

/// The parts of the workout document and its readers that a deferred start changes.
@Suite("Deferred start in the document")
struct DeferredStartDocumentTests {
    private static let localId = UUID()
    private static let plan = UUID()

    private static func aDeferredWorkout() -> ActiveWorkout {
        ActiveWorkout(
            destination: .deferredStart(localId: localId),
            startedAt: AWorkout.start,
            sync: .pending,
            sets: [AWorkout.lift()],
            plannedSessionId: plan)
    }

    @Test("A deferred start survives the document, with the plan it came from")
    func itRoundTrips() throws {
        let workout = Self.aDeferredWorkout()

        let read = try JSONDecoder().decode(ActiveWorkout.self, from: JSONEncoder().encode(workout))

        #expect(read == workout)
        #expect(read.version == ActiveWorkout.currentVersion)
    }

    @Test("A version 3 document reads with no plan")
    func aVersion3DocumentHasNoPlan() throws {
        let json = """
            {"version":3,"destination":{"kind":"liveSession","sessionId":"11111111-1111-1111-1111-111111111111"},
             "startedAt":1,"sync":{"kind":"synced"},"sets":[],"rest":{"kind":"idle"},
             "prescriptions":[],"addedMovementDataIds":[],"notes":""}
            """

        let read = try JSONDecoder().decode(ActiveWorkout.self, from: Data(json.utf8))

        #expect(read.plannedSessionId == nil)
        #expect(read.destination == .liveSession(AWorkout.session))
    }

    @Test("Opening moves everything logged onto the session")
    func openingKeepsTheWork() {
        let deferred = Self.aDeferredWorkout()

        let opened = deferred.opened(as: AWorkout.session)

        #expect(opened.destination == .liveSession(AWorkout.session))
        #expect(opened.sets == deferred.sets)
        #expect(opened.plannedSessionId == Self.plan)
    }

    @Test("A workout logged against its own plan has not left it")
    func onItsPlan() {
        #expect(!Self.aDeferredWorkout().opened(as: Self.plan).hasLeftItsPlan)
        // Not yet opened says nothing either way.
        #expect(!Self.aDeferredWorkout().hasLeftItsPlan)
    }

    @Test("A workout from a plan logged as another session has left it")
    func offItsPlan() {
        #expect(Self.aDeferredWorkout().opened(as: AWorkout.session).hasLeftItsPlan)
    }

    @Test("A deferred start is as live as any other, and pushes as it goes")
    func itIsLive() {
        let destination = WorkoutDestination.deferredStart(localId: Self.localId)

        #expect(destination.isLive)
        #expect(destination.pushesAsItGoes)
        #expect(destination.sessionId == nil)
    }

    @Test("The Lock Screen keys a deferred start by its own id until the server gives it one")
    func theActivityIsKeyedByTheLocalId() throws {
        let projection = try #require(WorkoutActivityProjection.of(
            Self.aDeferredWorkout(), movementTitle: nil, isPersisted: true, at: AWorkout.start))

        #expect(projection.attributes.sessionId == Self.localId)
        #expect(projection.attributes.startedAt == AWorkout.start)
    }

    @Test("Signing out with a deferred start loses a workout the server has never seen")
    func signingOutLosesIt() {
        #expect(SignOutConsequence.of(Self.aDeferredWorkout()) == .unopenedWorkoutLost)
    }

    @Test("Signing out counts the finished workouts not yet delivered")
    func signingOutCountsTheOutbox() {
        #expect(SignOutConsequence.nothingInFlight.message(undelivered: 0) == "Anything logged is on the server.")
        #expect(SignOutConsequence.nothingInFlight.message(undelivered: 2)
            == "2 finished workouts not yet on the server are lost.")
        #expect(SignOutConsequence.unopenedWorkoutLost.message(undelivered: 1).hasSuffix(
            "A finished workout not yet on the server is lost."))
    }

    @Test("The plan offers nothing the device is already holding")
    func thePlanLeavesOutHeldSessions() {
        let held = UUID()
        let free = UUID()
        let today = CalendarDay(year: 2026, month: 11, day: 2)
        let plan = TrainingPlan.reading(
            sessions: [
                ARecordingServer.aPlannedSession(id: held),
                ARecordingServer.aPlannedSession(id: free),
                ARecordingServer.anActiveSession(id: AWorkout.session),
            ],
            today: today)
        let holding = [
            Self.aDeferredWorkout(),
            ActiveWorkout(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start, plannedSessionId: held),
        ]

        let left = plan.excluding(TrainingPlan.sessionIds(heldBy: holding))

        #expect(left.today.map(\.envelope.trainingSessionId) == [free])
        #expect(left.inProgress == nil)
    }
}
