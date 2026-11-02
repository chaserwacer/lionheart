import Foundation
import Testing

@testable import Lionheart

/// The persistence the workout bar's seam is built on.
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

    @Test("A workout written is a workout read back")
    func aWorkoutRoundTrips() throws {
        let document = Self.aDocument()
        let workout = ActiveWorkout(
            sessionId: UUID(uuidString: "99999999-9999-9999-9999-999999999999")!,
            startedAt: Date(timeIntervalSince1970: 1_772_645_400))

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
        document.write(ActiveWorkout(sessionId: UUID(), startedAt: Date()))

        document.clear()

        #expect(document.read() == nil)
    }

    @Test("Truncated JSON reads as no workout rather than throwing")
    func aCorruptDocumentIsTreatedAsAbsent() throws {
        // There is no screen on which "your saved workout is corrupt" helps, and the app has to be
        // able to start.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try Data(#"{"sessionId":"not-a-uuid"#.utf8)
            .write(to: directory.appending(path: "active-workout.json"))

        #expect(ActiveWorkoutDocument(directoryURL: directory).read() == nil)
    }

    @Test("A document from a newer build is refused rather than half-read")
    func aNewerDocumentIsRefused() throws {
        // A downgraded build that half-understood a future shape would write it back truncated,
        // which is how logged sets get lost for good.
        let directory = URL.temporaryDirectory.appending(path: UUID().uuidString)
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let fromTheFuture = ActiveWorkout(
            sessionId: UUID(), startedAt: Date(), version: ActiveWorkout.currentVersion + 1)
        try JSONEncoder().encode(fromTheFuture)
            .write(to: directory.appending(path: "active-workout.json"))

        #expect(ActiveWorkoutDocument(directoryURL: directory).read() == nil)
    }

    @Test("Writing twice leaves the second workout, not both")
    func aWriteReplacesWhatWasThere() {
        let document = Self.aDocument()
        let first = ActiveWorkout(sessionId: UUID(), startedAt: Date(timeIntervalSince1970: 1))
        let second = ActiveWorkout(sessionId: UUID(), startedAt: Date(timeIntervalSince1970: 2))

        document.write(first)
        document.write(second)

        #expect(document.read() == second)
    }
}

/// The value itself.
@Suite("Active workout")
struct ActiveWorkoutTests {
    @Test("Elapsed time counts from the start")
    func elapsedCountsFromTheStart() {
        let workout = ActiveWorkout(
            sessionId: UUID(), startedAt: Date(timeIntervalSince1970: 1000))

        #expect(workout.elapsed(at: Date(timeIntervalSince1970: 1_720)) == 720)
    }

    @Test("A clock that moved backwards does not make a workout count down")
    func elapsedNeverGoesNegative() {
        let workout = ActiveWorkout(
            sessionId: UUID(), startedAt: Date(timeIntervalSince1970: 1000))

        #expect(workout.elapsed(at: Date(timeIntervalSince1970: 900)) == 0)
    }

    @Test("Marking synced changes only that, and keeps the identity and the start")
    func markingSyncedChangesOnlyTheFlag() {
        let workout = ActiveWorkout(
            sessionId: UUID(uuidString: "99999999-9999-9999-9999-999999999999")!,
            startedAt: Date(timeIntervalSince1970: 1000))

        let unsynced = workout.markingSynced(false)

        #expect(unsynced.isSynced == false)
        #expect(unsynced.sessionId == workout.sessionId)
        #expect(unsynced.startedAt == workout.startedAt)
        #expect(unsynced.version == workout.version)
    }

    @Test("A workout of this build's version is readable")
    func thisBuildsVersionIsReadable() {
        #expect(ActiveWorkout(sessionId: UUID(), startedAt: Date()).isReadable)
        #expect(!ActiveWorkout(
            sessionId: UUID(), startedAt: Date(),
            version: ActiveWorkout.currentVersion + 1).isReadable)
    }
}

/// The store the bar is driven by.
@MainActor
@Suite("Active workout store")
struct ActiveWorkoutStoreTests {
    private static func aStore() -> (ActiveWorkoutStore, ActiveWorkoutDocument) {
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        return (ActiveWorkoutStore(document: document, now: { Date(timeIntervalSince1970: 500) }),
                document)
    }

    @Test("Nothing in flight is nothing to render")
    func anEmptyStoreHasNoWorkout() {
        let (store, _) = Self.aStore()

        #expect(store.workout == nil)
        #expect(store.isPersisted)
    }

    @Test("Beginning a workout persists it before publishing it")
    func beginningPersistsFirst() {
        let (store, document) = Self.aStore()
        let sessionId = UUID(uuidString: "99999999-9999-9999-9999-999999999999")!

        store.begin(sessionId: sessionId)

        // The value on screen and the value on disk are the same one. Publishing first would let a
        // crash in between lose something the athlete had already seen confirmed.
        #expect(store.workout?.sessionId == sessionId)
        #expect(document.read() == store.workout)
        #expect(store.isPersisted)
    }

    @Test("A workout begun takes the injected clock, not the wall clock")
    func beginningUsesTheInjectedClock() {
        let (store, _) = Self.aStore()

        store.begin(sessionId: UUID())

        #expect(store.workout?.startedAt == Date(timeIntervalSince1970: 500))
    }

    @Test("What survived the last run is what is restored")
    func restoringReadsWhatSurvived() async {
        let (store, document) = Self.aStore()
        let workout = ActiveWorkout(
            sessionId: UUID(uuidString: "99999999-9999-9999-9999-999999999999")!,
            startedAt: Date(timeIntervalSince1970: 1000))
        document.write(workout)

        await store.restore()

        #expect(store.workout == workout)
    }

    @Test("Ending clears the document, because that is what makes it unrecoverable")
    func endingClearsTheDocument() {
        let (store, document) = Self.aStore()
        store.begin(sessionId: UUID())

        store.end()

        #expect(store.workout == nil)
        #expect(document.read() == nil)
    }

    @Test("An unsynced workout is still persisted, because those are different facts")
    func syncAndPersistenceAreSeparate() {
        let (store, document) = Self.aStore()
        store.begin(sessionId: UUID())

        store.markSynced(false)

        #expect(store.workout?.isSynced == false)
        #expect(store.isPersisted)
        #expect(document.read()?.isSynced == false)
    }

    @Test("Marking the sync state it already has changes nothing")
    func markingTheSameSyncStateIsANoOp() {
        let (store, _) = Self.aStore()
        store.begin(sessionId: UUID())
        let before = store.workout

        store.markSynced(true)

        #expect(store.workout == before)
    }
}
