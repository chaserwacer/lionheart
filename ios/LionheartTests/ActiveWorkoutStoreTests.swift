import Foundation
import Testing

@testable import Lionheart

/// A server that answers however a test needs it to.
///
/// It stands in for the one collaborator a unit test cannot run. It records what it was asked to
/// write, because several of the claims below are about *what reached the server* rather than about
/// what the store returned.
private final class AServer: WorkoutSessionWriting, @unchecked Sendable {
    /// Guarded by the main actor: every call into it comes from `ActiveWorkoutStore`, which is
    /// `@MainActor`, and the tests that read it are too.
    private(set) var pushedSets: [[SetCommand]] = []
    private(set) var completions = 0
    private(set) var logged: [[SetCommand]] = []
    private(set) var notes: [String] = []
    private(set) var deletions: [UUID] = []
    private(set) var efforts: [PerceivedEffortRequest?] = []

    var failPushes: (any Error)?
    var failCompletion: (any Error)?

    /// What a read of the session answers. `nil` makes the read itself fail, which is the case the
    /// store must treat as "I cannot tell" rather than as "not completed".
    var sessionOnServer: TrainingSession?
    private(set) var sessionReads: [UUID] = []

    func session(id: UUID) async throws -> TrainingSession {
        sessionReads.append(id)
        guard let sessionOnServer else { throw APIError.transport("the read itself failed") }
        return sessionOnServer
    }

    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession {
        pushedSets.append(sets)
        if let failPushes { throw failPushes }
        return Self.anActiveSession(id: id)
    }

    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession {
        self.notes.append(notes)
        if let failCompletion { throw failCompletion }
        return Self.anActiveSession(id: id)
    }

    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession {
        if let failCompletion { throw failCompletion }
        efforts.append(effort)
        completions += 1
        return Self.anActiveSession(id: id)
    }

    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String,
        programId: UUID?
    ) async throws -> TrainingSession {
        if let failCompletion { throw failCompletion }
        efforts.append(effort)
        logged.append(sets)
        return Self.anActiveSession(id: AWorkout.session)
    }

    func delete(id: UUID) async throws {
        deletions.append(id)
    }

    fileprivate static func aCompletedSession(id: UUID) -> TrainingSession {
        .completed(TrainingSession.Completed(
            envelope: TrainingSession.Envelope(
                trainingSessionId: id,
                notes: "",
                provenance: .user,
                trainingProgramId: nil,
                createdAt: nil,
                date: CalendarDay(year: 2026, month: 11, day: 2)),
            sets: [],
            period: .instant(at: AWorkout.start),
            effort: .notReported,
            decoration: .none))
    }

    private static func anActiveSession(id: UUID) -> TrainingSession {
        .active(TrainingSession.Active(
            envelope: TrainingSession.Envelope(
                trainingSessionId: id,
                notes: "",
                provenance: .user,
                trainingProgramId: nil,
                createdAt: nil,
                date: CalendarDay(year: 2026, month: 11, day: 2)),
            startedAt: AWorkout.start,
            sets: []))
    }
}

/// The store the whole feature rests on.
///
/// The claims here are the three the type exists to keep: a set reaches disk before it reaches the
/// screen, a failed push is a state rather than a loss, and only a server that accepted the session
/// gets to make the local copy unrecoverable.
@MainActor
@Suite("Active workout store")
struct ActiveWorkoutStoreTests {
    private static func aStore(
        server: AServer = AServer(), at moment: TimeInterval = 0
    ) -> (ActiveWorkoutStore, ActiveWorkoutDocument, AServer) {
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        let store = ActiveWorkoutStore(
            document: document,
            sessions: server,
            now: { AWorkout.start.addingTimeInterval(moment) })
        return (store, document, server)
    }

    // MARK: - Beginning

    @Test("Nothing in flight is nothing to render")
    func anEmptyStoreHasNoWorkout() {
        let (store, _, _) = Self.aStore()

        #expect(store.workout == nil)
        #expect(store.isPersisted)
    }

    @Test("Beginning a workout persists it before publishing it")
    func beginningPersistsFirst() {
        let (store, document, _) = Self.aStore()

        #expect(store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start))

        // The value on screen and the value on disk are the same one. Publishing first would let a
        // crash in between lose something the athlete had already seen confirmed.
        #expect(store.workout?.destination == .liveSession(AWorkout.session))
        #expect(document.read() == store.workout)
        #expect(store.isPersisted)
    }

    @Test("A second workout is refused while one is in flight")
    func aSecondWorkoutIsRefused() {
        // The server would create a second active session quite happily, and the device can hold
        // only one — so the extra session would be an orphan nobody could reach.
        let (store, _, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        let first = store.workout

        let began = store.begin(destination: .afterTheFact, startedAt: AWorkout.start)

        #expect(!began)
        #expect(store.workout == first)
    }

    @Test("A workout begun with resumed sets starts out synced, because the server has them")
    func resumedSetsAreNotPushedAgain() {
        let (store, _, server) = Self.aStore()

        store.begin(
            destination: .liveSession(AWorkout.session),
            startedAt: AWorkout.start,
            sets: [AWorkout.lift()])

        #expect(store.workout?.sync == .synced)
        #expect(server.pushedSets.isEmpty)
    }

    @Test("What survived the last run is what is restored")
    func restoringReadsWhatSurvived() async {
        let (store, document, _) = Self.aStore()
        let workout = AWorkout.live(sets: [AWorkout.lift(), AWorkout.run()])
        document.write(workout)

        await store.restore()

        #expect(store.workout == workout)
    }

    // MARK: - Choosing a movement

    @Test("A movement picked in an unplanned workout is on disk, and still there after a restart")
    func aPickedMovementSurvivesARestart() async throws {
        let (store, document, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        store.add(AWorkout.bench)

        #expect(document.read()?.addedMovementDataIds == [AWorkout.bench])
        // Choosing a movement changes nothing the server holds.
        #expect(server.pushedSets.isEmpty)

        let relaunched = ActiveWorkoutStore(document: document, sessions: AServer())
        await relaunched.restore()

        let restored = try #require(relaunched.workout)
        #expect(WorkoutReading.of(restored).movements.map(\.movementDataId) == [AWorkout.bench])
        #expect(restored.currentMovementDataId == AWorkout.bench)
    }

    @Test("Removing a pick with nothing logged is persisted too")
    func droppingAPickIsPersisted() {
        let (store, document, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.add(AWorkout.bench)

        store.drop(AWorkout.bench)

        #expect(store.workout?.addedMovementDataIds.isEmpty == true)
        #expect(document.read() == store.workout)
    }

    // MARK: - Logging

    @Test("A logged set is on disk, not only on screen")
    func aLoggedSetIsPersisted() {
        let (store, document, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        store.log(
            .lift(SetCommand.Lift(
                movementDataId: AWorkout.bench, reps: 5, weight: 100,
                unit: .kilograms, rpe: .rated(8))),
            restingFor: 120)

        #expect(store.workout?.sets.count == 1)
        #expect(document.read()?.sets.count == 1)
        #expect(document.read()?.sets.first?.command.liftPart?.weight == 100)
    }

    @Test("Logging with no workout in flight does nothing at all")
    func loggingWithoutAWorkoutIsANoOp() {
        let (store, document, _) = Self.aStore()

        store.log(.freeform(text: "x"), restingFor: nil)

        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    @Test("A set logged against a workout being written down afterwards is never pushed")
    func aRetroLogDoesNotPush() async {
        let (store, _, server) = Self.aStore()
        store.begin(destination: .afterTheFact, startedAt: AWorkout.start)

        store.log(.freeform(text: "some work"), restingFor: nil)
        await store.syncNow()

        #expect(server.pushedSets.isEmpty)
        #expect(store.workout?.sets.count == 1)
    }

    // MARK: - Reaching the server

    @Test("A successful push leaves the workout synced")
    func aSuccessfulPushMarksSynced() async {
        let (store, _, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        #expect(server.pushedSets.count == 1)
        #expect(store.workout?.sync == .synced)
    }

    @Test("A push that could not reach the server is a state, not a lost set")
    func aFailedPushKeepsTheSets() async {
        let (store, document, server) = Self.aStore()
        server.failPushes = APIError.transport("The network is not reachable.")
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        // The athlete's words, not URLSession's: the failure is restated into the feature's own
        // vocabulary at the boundary, so nothing downstream shows a transport description.
        #expect(store.workout?.sync == .failed("Lionheart is not reachable."))
        #expect(store.workout?.sets.count == 1)
        #expect(document.read()?.sets.count == 1)
    }

    @Test("A 409 on a push is a refusal, not something to keep retrying")
    func aRefusedPushIsNotRetried() async {
        // The session is no longer active — completed or deleted somewhere else — and an identical
        // request cannot change that, so the athlete is told rather than the device spinning.
        let (store, _, server) = Self.aStore()
        server.failPushes = APIError.refused(detail: "A completed session cannot be given sets.")
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        #expect(store.workout?.sync == .refused("A completed session cannot be given sets."))
        #expect(store.workout?.sync.isWorthPushing == false)
    }

    @Test("A 404 on a push reads as a refusal too, because the session is gone")
    func aMissingSessionIsARefusal() async {
        let (store, _, server) = Self.aStore()
        server.failPushes = APIError.notFound(detail: nil)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        await store.syncNow()

        #expect(store.workout?.sync == .refused("This session is no longer on the server."))
    }

    @Test("A push carries the whole list, in the order the sets were performed")
    func aPushCarriesTheWholeListInOrder() async {
        let (store, _, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "first"), restingFor: nil)
        store.log(.freeform(text: "second"), restingFor: nil)

        await store.syncNow()

        #expect(server.pushedSets.last == [.freeform(text: "first"), .freeform(text: "second")])
    }

    // MARK: - Finishing

    @Test("Finishing a live session pushes the sets before completing it")
    func completingPushesFirst() async {
        // `/complete` carries only the period and the effort, so completing over an unsynced list
        // would complete the session without the sets in it.
        let (store, document, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let finished = await store.complete(
            effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(finished)
        #expect(server.pushedSets.last == [.freeform(text: "a set")])
        #expect(server.completions == 1)
        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    @Test("A completion the server refused leaves the workout and the document alone")
    func aRefusedCompletionKeepsEverything() async {
        let (store, document, server) = Self.aStore()
        server.failCompletion = APIError.refused(detail: "A completed session cannot be completed.")
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let finished = await store.complete(
            effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(!finished)
        #expect(store.workout?.sets.count == 1)
        #expect(document.read()?.sets.count == 1)
        #expect(store.finish == .refused(.refused("A completed session cannot be completed.")))
    }

    @Test("A completion that could not be pushed does not complete the session")
    func anUnpushableCompletionIsRefused() async {
        let (store, document, server) = Self.aStore()
        server.failPushes = APIError.transport("The network is not reachable.")
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let finished = await store.complete(
            effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(!finished)
        #expect(server.completions == 0)
        #expect(document.read()?.sets.count == 1)
    }

    @Test("A workout logged afterwards lands in one request, carrying its sets")
    func aRetroWorkoutLandsWhole() async {
        let (store, document, server) = Self.aStore()
        store.begin(destination: .afterTheFact, startedAt: AWorkout.start)
        store.log(.freeform(text: "a set"), restingFor: nil)

        let finished = await store.complete(
            effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(finished)
        #expect(server.pushedSets.isEmpty)
        #expect(server.logged == [[.freeform(text: "a set")]])
        #expect(document.read() == nil)
    }

    @Test("Notes written about the session reach the server before it is completed")
    func notesAreSentBeforeCompleting() async {
        let (store, _, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.note("felt strong")

        _ = await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(server.notes == ["felt strong"])
    }

    @Test("An effort with nothing in it is sent as no effort at all")
    func anEmptyEffortIsNotReported() async {
        // The server treats an empty command and an absent one as the same domain fact, and the
        // store must not turn "did not rate it" into "rated it with nothing".
        let (store, _, server) = Self.aStore()
        store.begin(destination: .afterTheFact, startedAt: AWorkout.start)

        _ = await store.complete(
            effort: .unrated, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(server.efforts == [nil])
    }

    // MARK: - Discarding and signing out

    @Test("Discarding removes the session and the local copy")
    func discardingRemovesBoth() async {
        let (store, document, server) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        await store.discard()

        #expect(server.deletions == [AWorkout.session])
        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    /// The one path that can lose a whole workout for good, so it is the one most worth a test.
    ///
    /// Completing can commit on the server and lose only its *response* — a dropped connection, a
    /// body that would not decode. The device then still believes the workout is unfinished and
    /// offers the athlete "discard", which used to delete a session the server had saved with
    /// every set in it. The athlete would be throwing away what they had just finished, while
    /// being told they were throwing away something unsaved.
    @Test("Discarding never deletes a workout the server has already saved")
    func discardingNeverDeletesASavedWorkout() async {
        let (store, document, server) = Self.aStore()
        server.sessionOnServer = AServer.aCompletedSession(id: AWorkout.session)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        await store.discard()

        #expect(server.deletions.isEmpty)
        // The local copy still goes: the server is holding the real one.
        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    @Test("A completion whose response was lost counts as completed, not refused")
    func aLostCompletionResponseIsNotAFailure() async {
        let (store, document, server) = Self.aStore()
        server.failCompletion = APIError.transport("the response never arrived")
        server.sessionOnServer = AServer.aCompletedSession(id: AWorkout.session)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        let landed = await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(landed)
        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    /// A read that fails answers "I cannot tell", which has to behave as "not completed" — keeping
    /// the local copy is the recoverable mistake, and clearing it on a guess is not.
    @Test("A session read that fails leaves the workout alone")
    func anUnreadableSessionKeepsTheWorkout() async {
        let (store, document, server) = Self.aStore()
        server.failCompletion = APIError.transport("the response never arrived")
        server.sessionOnServer = nil
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        let landed = await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600))

        #expect(!landed)
        #expect(store.workout != nil)
        #expect(document.read() != nil)
    }

    @Test("Signing out takes the document with it")
    func signingOutClearsTheDocument() {
        // A workout belongs to the session the signed-out credential owned. One the next sign-in
        // could adopt would push one athlete's sets at another athlete's session id.
        let (store, document, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        store.reset()

        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    // MARK: - The rest clock

    @Test("Starting a rest persists it, so a force-quit mid-rest resumes it")
    func aRestIsPersisted() {
        let (store, document, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        store.startRest(120)

        #expect(document.read()?.rest == .running(since: AWorkout.start, duration: 120))
    }

    @Test("Extending a rest lengthens it rather than restarting it")
    func extendingDoesNotRestart() {
        let (store, _, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.startRest(120)

        store.extendRest(by: 30)

        #expect(store.workout?.rest == .running(since: AWorkout.start, duration: 150))
    }

    @Test("Skipping a rest puts the clock away")
    func skippingClearsTheClock() {
        let (store, _, _) = Self.aStore()
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.startRest(120)

        store.skipRest()

        #expect(store.workout?.rest == .idle)
    }

    @Test("A rest that ran out while the app was away is tidied up without announcing itself")
    func anExpiredRestIsRetiredSilently() async {
        // Reading the document is not the moment to buzz: the athlete has been back for some time
        // and the rest was over before they were.
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        document.write(AWorkout.live(rest: .running(since: AWorkout.start, duration: 60)))
        var announced = 0
        let store = ActiveWorkoutStore(
            document: document,
            sessions: AServer(),
            now: { AWorkout.start.addingTimeInterval(600) },
            onRestFinished: { announced += 1 })

        await store.restore()

        #expect(store.workout?.rest == .idle)
        #expect(announced == 0)
    }
}
