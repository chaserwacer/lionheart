import Foundation
import Testing

@testable import Lionheart

/// A live workout started with no connection: logged like any other, its session opened by the
/// first push that reaches the server, and never opened twice.
@MainActor
@Suite("Deferred start")
struct DeferredStartTests {
    private static let localId = UUID(uuidString: "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")!
    private static let plan = UUID(uuidString: "BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB")!

    private static func aStore(
        _ server: ARecordingServer = ARecordingServer()
    ) -> (ActiveWorkoutStore, ActiveWorkoutDocument, ARecordingServer, WorkoutOutbox) {
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        let (outbox, _) = anOutbox(sending: server)
        let store = ActiveWorkoutStore(
            document: document, sessions: server, outbox: outbox, now: { AWorkout.start })
        return (store, document, server, outbox)
    }

    private static func beginDeferred(
        _ store: ActiveWorkoutStore, fromPlan plan: UUID? = nil
    ) {
        store.begin(
            destination: .deferredStart(localId: localId),
            startedAt: AWorkout.start,
            plannedSessionId: plan)
    }

    // MARK: - Opening

    @Test("A workout started offline opens its session the first time the server answers")
    func theFirstPushOpensTheSession() async {
        let (store, document, server, _) = Self.aStore()
        Self.beginDeferred(store)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        // Opened at the instant the athlete started, not when the server happened to be reached.
        #expect(server.unplannedStarts == [AWorkout.start])
        #expect(store.workout?.destination == .liveSession(ARecordingServer.opened))
        #expect(server.pushedSets.last == [.freeform(text: "a set")])
        #expect(store.workout?.sync == .synced)
        // On disk too, so a relaunch carries on as an ordinary live workout.
        #expect(document.read()?.destination == .liveSession(ARecordingServer.opened))
    }

    @Test("While the server cannot be reached the workout waits, sets and all")
    func anUnreachableServerLeavesItWaiting() async {
        let (store, document, server, _) = Self.aStore()
        server.sessionsAround = nil
        Self.beginDeferred(store)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        #expect(store.workout?.destination == .deferredStart(localId: Self.localId))
        #expect(store.workout?.sync == .failed("Lionheart is not reachable."))
        #expect(document.read()?.sets.count == 1)
        #expect(server.unplannedStarts.isEmpty)
    }

    @Test("An opening that failed does not stand in for the next one")
    func aFailedOpeningIsTriedAgain() async {
        // Found running the app: the failed attempt was left held, and every later push awaited it
        // and failed with it, so the workout never reached the server once it came back.
        let (store, _, server, _) = Self.aStore()
        server.failOpening = APIError.transport("The network is not reachable.")
        Self.beginDeferred(store)
        await store.syncNow()

        server.failOpening = nil
        await store.serverReturned()

        #expect(store.workout?.destination == .liveSession(ARecordingServer.opened))
        #expect(store.workout?.sync == .synced)
    }

    @Test("A start that landed without its answer is found again, not opened twice")
    func aLandedStartIsRecognised() async {
        let landed = UUID()
        let server = ARecordingServer()
        server.sessionsAround = [ARecordingServer.anActiveSession(id: landed, startedAt: AWorkout.start)]
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store)

        await store.syncNow()

        #expect(server.unplannedStarts.isEmpty)
        #expect(store.workout?.destination == .liveSession(landed))
    }

    @Test("Another workout active at a different start is not mistaken for this one")
    func aDifferentStartIsSomebodyElses() async {
        let server = ARecordingServer()
        server.sessionsAround = [
            ARecordingServer.anActiveSession(id: UUID(), startedAt: AWorkout.start.addingTimeInterval(-40)),
        ]
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store)

        await store.syncNow()

        #expect(server.unplannedStarts.count == 1)
        #expect(store.workout?.destination == .liveSession(ARecordingServer.opened))
    }

    @Test("Two pushes at once open one session")
    func overlappingPushesShareOneOpening() async {
        let server = ARecordingServer()
        server.openingTakes = .milliseconds(150)
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store)

        async let first: Void = store.syncNow()
        async let second: Void = store.serverReturned()
        _ = await (first, second)

        #expect(server.unplannedStarts.count == 1)
    }

    @Test("A deferred start from a plan starts that plan")
    func aStillPlannedPlanIsStarted() async {
        let server = ARecordingServer()
        server.sessionOnServer = ARecordingServer.aPlannedSession(id: Self.plan)
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store, fromPlan: Self.plan)

        await store.syncNow()

        #expect(server.plannedStarts == [Self.plan])
        #expect(server.unplannedStarts.isEmpty)
        #expect(store.workout?.destination == .liveSession(Self.plan))
        #expect(store.workout?.hasLeftItsPlan == false)
    }

    @Test("A plan skipped elsewhere is not this workout's, which opens a session of its own")
    func aLostPlanOpensItsOwnSession() async {
        let server = ARecordingServer()
        server.sessionOnServer = ARecordingServer.aSkippedSession(id: Self.plan)
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store, fromPlan: Self.plan)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        // The sets are real whatever became of the plan.
        #expect(server.plannedStarts.isEmpty)
        #expect(store.workout?.destination == .liveSession(ARecordingServer.opened))
        #expect(server.pushedSets.last == [.freeform(text: "a set")])
        #expect(store.workout?.hasLeftItsPlan == true)
    }

    @Test("A plan started elsewhere at another instant is lost too")
    func aPlanStartedElsewhereIsLost() async {
        let server = ARecordingServer()
        server.sessionOnServer = ARecordingServer.anActiveSession(
            id: Self.plan, startedAt: AWorkout.start.addingTimeInterval(-600))
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store, fromPlan: Self.plan)

        await store.syncNow()

        #expect(store.workout?.destination == .liveSession(ARecordingServer.opened))
    }

    @Test("A plan this workout already started is recognised by its start")
    func aPlanThisWorkoutStartedIsKept() async {
        let server = ARecordingServer()
        server.sessionOnServer = ARecordingServer.anActiveSession(id: Self.plan, startedAt: AWorkout.start)
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store, fromPlan: Self.plan)

        await store.syncNow()

        #expect(server.plannedStarts.isEmpty)
        #expect(store.workout?.destination == .liveSession(Self.plan))
    }

    // MARK: - Finishing

    @Test("Finished before the server was ever reached, it is set down for delivery")
    func finishingOfflineHoldsIt() async throws {
        let server = ARecordingServer()
        server.sessionsAround = nil
        let (store, document, _, outbox) = Self.aStore(server)
        Self.beginDeferred(store)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let outcome = await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3000))

        #expect(outcome == .heldForDelivery)
        #expect(store.workout == nil)
        #expect(document.read() == nil)
        let held = try #require(outbox.finished.first)
        #expect(held.workout.destination == .deferredStart(localId: Self.localId))
    }

    @Test("Finished once the server is back but before it was opened, it lands whole")
    func aNeverOpenedWorkoutLandsWhole() async {
        let (store, _, server, _) = Self.aStore()
        Self.beginDeferred(store)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let outcome = await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3000))

        // One request carrying everything, rather than opening a session only to complete it.
        #expect(outcome == .delivered)
        #expect(server.logged == [[.freeform(text: "a set")]])
        #expect(server.unplannedStarts.isEmpty)
        #expect(server.completions == 0)
    }

    @Test("Discarding a deferred start removes a session it opened without hearing back")
    func discardingRemovesWhatItOpened() async {
        let opened = UUID()
        let server = ARecordingServer()
        server.sessionsAround = [ARecordingServer.anActiveSession(id: opened, startedAt: AWorkout.start)]
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store)

        await store.discard()

        #expect(server.deletions == [opened])
        #expect(store.workout == nil)
    }

    @Test("Discarding a deferred start leaves the plan it came from planned")
    func discardingLeavesThePlan() async {
        let server = ARecordingServer()
        server.sessionOnServer = ARecordingServer.aPlannedSession(id: Self.plan)
        let (store, _, _, _) = Self.aStore(server)
        Self.beginDeferred(store, fromPlan: Self.plan)

        await store.discard()

        // The athlete threw away a workout, not the plan for it.
        #expect(server.deletions.isEmpty)
        #expect(store.workout == nil)
    }
}

/// Finished workouts the server has not taken yet.
@MainActor
@Suite("Workout outbox")
struct WorkoutOutboxTests {
    private static func aFinished(
        _ workout: ActiveWorkout = AWorkout.retro(sets: [AWorkout.words()])
    ) -> FinishedWorkout {
        FinishedWorkout(workout: workout, endedAt: AWorkout.start.addingTimeInterval(3600), effort: nil)
    }

    @Test("A finished workout set down survives a relaunch")
    func itSurvivesARelaunch() async {
        let server = ARecordingServer()
        let (outbox, document) = anOutbox(sending: server)
        let finished = Self.aFinished()

        #expect(outbox.hold(finished))

        let relaunched = WorkoutOutbox(document: document, sessions: server)
        await relaunched.restore()
        #expect(relaunched.finished == [finished])
    }

    @Test("Delivered, it is gone from the device")
    func aDeliveryRemovesIt() async {
        let server = ARecordingServer()
        let (outbox, document) = anOutbox(sending: server)
        #expect(outbox.hold(Self.aFinished()))

        await outbox.deliver()

        #expect(server.logged == [[AWorkout.words().command]])
        #expect(outbox.finished.isEmpty)
        #expect(document.read().isEmpty)
    }

    @Test("A workout the server already holds is not sent again")
    func anAlreadyDeliveredWorkoutIsNotDuplicated() async {
        // The earlier attempt landed and only its answer was lost; `POST /completed` sent again
        // would make a second session of the same sets.
        let server = ARecordingServer()
        server.sessionsAround = [ARecordingServer.aCompletedSession(id: UUID(), startedAt: AWorkout.start)]
        let (outbox, _) = anOutbox(sending: server)
        #expect(outbox.hold(Self.aFinished()))

        await outbox.deliver()

        #expect(server.logged.isEmpty)
        #expect(outbox.finished.isEmpty)
    }

    @Test("With the server still away it keeps waiting, and the ones behind it are not tried")
    func anUnreachableServerStopsTheRun() async {
        let server = ARecordingServer()
        server.sessionsAround = nil
        let (outbox, _) = anOutbox(sending: server)
        #expect(outbox.hold(Self.aFinished()))
        #expect(outbox.hold(Self.aFinished()))

        await outbox.deliver()

        #expect(outbox.finished.count == 2)
        let allWaiting = outbox.finished.allSatisfy(\.isWorthDelivering)
        #expect(allWaiting)
        #expect(server.windowReads == 1)
    }

    @Test("One the server refuses waits for the athlete, with the server's reason")
    func aRefusalIsKeptAndStated() async {
        let server = ARecordingServer()
        server.failCompletion = APIError.refused(detail: "A completed session cannot be completed.")
        let (outbox, document) = anOutbox(sending: server)
        #expect(outbox.hold(Self.aFinished()))

        await outbox.deliver()

        #expect(outbox.finished.map(\.delivery) == [.refused("A completed session cannot be completed.")])
        #expect(document.read().map(\.delivery) == [.refused("A completed session cannot be completed.")])
        #expect(!outbox.hasDeliverableWork)
    }

    @Test("Only a refused workout can be discarded")
    func onlyARefusalIsDiscardable() async {
        let server = ARecordingServer()
        server.sessionsAround = nil
        let (outbox, _) = anOutbox(sending: server)
        let waiting = Self.aFinished()
        #expect(outbox.hold(waiting))

        outbox.discard(waiting.id)

        // Still on its way: discarding it would throw away work nobody refused.
        #expect(outbox.finished.map(\.id) == [waiting.id])
    }

    @Test("Signing out takes every finished workout with it")
    func signingOutEmptiesIt() {
        let (outbox, document) = anOutbox(sending: ARecordingServer())
        #expect(outbox.hold(Self.aFinished()))

        outbox.reset()

        #expect(outbox.finished.isEmpty)
        #expect(document.read().isEmpty)
    }
}
