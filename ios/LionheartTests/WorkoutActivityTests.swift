import Foundation
import Testing

@testable import Lionheart

/// A Lock Screen that records what it was asked to show.
///
/// It stands in for ActivityKit, which is another process and cannot be run from a unit test. The
/// claims below are about the *sequence* it receives, because the failure this feature can
/// actually have is an activity that outlives the workout it mirrors — which is a fact about the
/// last thing the store said, not about any single call.
@MainActor
private final class ALockScreen: WorkoutActivityPresenting {
    private(set) var shown: [WorkoutActivityProjection?] = []

    /// Set to make the first call take longer than the second would, which is how the test for
    /// ordering proves the chain rather than observing a scheduler that happened to behave.
    var holdFirstCall = false
    private var hasHeld = false

    func present(_ projection: WorkoutActivityProjection?) async {
        if holdFirstCall, !hasHeld {
            hasHeld = true
            try? await Task.sleep(for: .milliseconds(120))
        }
        shown.append(projection)
    }

    var last: WorkoutActivityProjection?? { shown.last }
}

@MainActor
@Suite("Workout activity projection")
struct WorkoutActivityProjectionTests {
    private static let now = AWorkout.start.addingTimeInterval(600)

    @Test("A live workout is worth a Lock Screen")
    func liveWorkoutProjects() {
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(), movementTitle: "Back Squat", isPersisted: true, at: Self.now)

        #expect(projection?.attributes.sessionId == AWorkout.session)
        #expect(projection?.attributes.startedAt == AWorkout.start)
        #expect(projection?.state.movementTitle == "Back Squat")
    }

    @Test("A workout written down afterwards gets no Lock Screen")
    func retroWorkoutProjectsNothing() {
        // Not an omission: a retro log has no elapsed clock the athlete wants ticking and no rest
        // to count, and they are holding the phone to type it.
        #expect(WorkoutActivityProjection.of(
            AWorkout.retro(), movementTitle: "Back Squat", isPersisted: true, at: Self.now) == nil)
    }

    @Test("No workout is nothing to show")
    func noWorkoutProjectsNothing() {
        #expect(WorkoutActivityProjection.of(
            nil, movementTitle: nil, isPersisted: true, at: Self.now) == nil)
    }

    @Test("A running rest becomes the range the system counts over")
    func runningRestBecomesAnInterval() {
        let began = Self.now.addingTimeInterval(-30)
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(rest: .running(since: began, duration: 120)),
            movementTitle: nil, isPersisted: true, at: Self.now)

        #expect(projection?.state.rest?.startedAt == began)
        #expect(projection?.state.rest?.endsAt == began.addingTimeInterval(120))
    }

    @Test("A rest whose time is up reads as working, not as resting for zero")
    func expiredRestProjectsNoRest() {
        // The store retires an expired clock a moment later. Until it does, the Lock Screen must
        // not claim a countdown — and a zero-length range would trap on construction besides.
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(rest: .running(since: Self.now.addingTimeInterval(-120), duration: 60)),
            movementTitle: nil, isPersisted: true, at: Self.now)

        #expect(projection?.state.rest == nil)
    }

    @Test("A rest of no length is refused rather than built")
    func zeroLengthRestIsRefused() {
        #expect(WorkoutActivityAttributes.Rest(startedAt: Self.now, endsAt: Self.now) == nil)
    }

    @Test("An unsaved workout is the concern that leads")
    func unpersistedLeadsTheConcern() {
        // Ahead of the sync state deliberately: losing the workout to a force-quit is the only one
        // of the two the athlete can do something about.
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(sync: .refused("This session was completed elsewhere.")),
            movementTitle: nil, isPersisted: false, at: Self.now)

        #expect(projection?.state.concern == "Not saved to this device")
    }

    @Test("A refusal is stated in the server's own words")
    func refusalIsStated() {
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(sync: .refused("This session is no longer on the server.")),
            movementTitle: nil, isPersisted: true, at: Self.now)

        #expect(projection?.state.concern == "This session is no longer on the server.")
    }

    @Test("Pending and synced say nothing")
    func ordinarySyncStatesAreSilent() {
        // Almost every set is pending for a second or two; a warning for it would teach the
        // athlete to ignore the line.
        for sync in [WorkoutSyncState.pending, .synced] {
            let projection = WorkoutActivityProjection.of(
                AWorkout.live(sync: sync), movementTitle: nil, isPersisted: true, at: Self.now)
            #expect(projection?.state.concern == nil)
        }
    }

    @Test("An empty movement title is an absent one")
    func emptyMovementTitleIsNil() {
        let projection = WorkoutActivityProjection.of(
            AWorkout.live(), movementTitle: "", isPersisted: true, at: Self.now)

        #expect(projection?.state.movementTitle == nil)
    }
}

/// What the store tells the Lock Screen, and when.
///
/// These are the claims that keep the two representations of one workout from disagreeing. Each
/// asserts the **last** thing the store said, because that is what is left on screen.
@MainActor
@Suite("The workout's Lock Screen lifecycle")
struct WorkoutActivityLifecycleTests {
    private static func aStore(
        _ lockScreen: ALockScreen, server: AnAgreeableServer = AnAgreeableServer()
    ) -> (ActiveWorkoutStore, ActiveWorkoutDocument) {
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        let store = ActiveWorkoutStore(
            document: document,
            sessions: server,
            activity: lockScreen,
            now: { AWorkout.start },
            movementTitle: { _ in "Back Squat" })
        return (store, document)
    }

    /// The mirror is an unstructured chain the store owns, so a test has to let it run.
    private static func settle() async {
        for _ in 0..<8 { await Task.yield() }
        try? await Task.sleep(for: .milliseconds(300))
    }

    @Test("Beginning a workout puts it on the Lock Screen")
    func beginningShowsIt() async {
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)

        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        await Self.settle()

        #expect(lockScreen.last??.attributes.sessionId == AWorkout.session)
    }

    @Test("Signing out takes the Lock Screen with it")
    func signingOutEndsIt() async {
        // `reset` is what AuthStore.signOut runs. An activity surviving it would leave the last
        // athlete's session counting down on a signed-out phone.
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        await Self.settle()

        store.reset()
        await Self.settle()

        #expect(lockScreen.last == .some(nil))
    }

    @Test("Discarding a workout takes the Lock Screen with it")
    func discardingEndsIt() async {
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        await Self.settle()

        await store.discard()
        await Self.settle()

        #expect(lockScreen.last == .some(nil))
    }

    @Test("A completed workout takes the Lock Screen with it")
    func completingEndsIt() async {
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        await Self.settle()

        #expect(await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600)))
        await Self.settle()

        #expect(lockScreen.last == .some(nil))
    }

    @Test("A launch that finds no workout clears whatever survived")
    func restoringNothingEndsIt() async {
        // ActivityKit activities outlive the app, so a launch with an empty document has to say so
        // rather than stay quiet — this is the call that reconciles a workout completed or
        // discarded while the app was not running.
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)

        await store.restore()
        await Self.settle()

        #expect(lockScreen.shown.count >= 1)
        #expect(lockScreen.last == .some(nil))
    }

    @Test("A launch that finds a workout puts it back on the Lock Screen")
    func restoringAWorkoutShowsIt() async {
        let lockScreen = ALockScreen()
        let (store, document) = Self.aStore(lockScreen)
        document.write(AWorkout.live())

        await store.restore()
        await Self.settle()

        #expect(lockScreen.last??.attributes.sessionId == AWorkout.session)
    }

    @Test("Arming a rest reaches the Lock Screen as a countdown")
    func armingARestShowsTheCountdown() async {
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)

        store.startRest(90)
        await Self.settle()

        #expect(lockScreen.last??.state.rest?.endsAt == AWorkout.start.addingTimeInterval(90))
    }

    @Test("Skipping a rest takes the countdown off the Lock Screen")
    func skippingARestClearsTheCountdown() async {
        let lockScreen = ALockScreen()
        let (store, _) = Self.aStore(lockScreen)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.startRest(90)
        await Self.settle()

        store.skipRest()
        await Self.settle()

        #expect(lockScreen.last??.state.rest == nil)
    }

    @Test("A slow update cannot land after the one that followed it")
    func updatesArriveInOrder() async {
        // The failure this prevents: two mutations a few milliseconds apart whose cross-process
        // writes race, leaving a countdown on screen for a rest the athlete has already skipped.
        let lockScreen = ALockScreen()
        lockScreen.holdFirstCall = true
        let (store, _) = Self.aStore(lockScreen)

        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        store.startRest(90)
        store.skipRest()
        await Self.settle()

        #expect(lockScreen.shown.count == 3)
        #expect(lockScreen.shown[1]?.state.rest != nil)
        #expect(lockScreen.last??.state.rest == nil)
    }
}
