import Foundation
import Testing

@testable import Lionheart

/// A notification centre that records what it was asked to hold.
///
/// It stands in for the system, which a unit test cannot run. The claims are about what is left
/// scheduled after a sequence of events, because the defect this feature can actually have is an
/// alarm outliving its reason — buzzing about a rest the athlete skipped, or a workout they threw
/// away.
@MainActor
private final class ANotificationCentre: RestAlarmScheduling {
    private(set) var scheduled: [PendingRestAlarm?] = []
    private(set) var forgotDelivered = 0

    func schedule(_ alarm: PendingRestAlarm?) async { scheduled.append(alarm) }
    func forgetDelivered() async { forgotDelivered += 1 }

    /// What would be waiting to fire now.
    var pending: PendingRestAlarm?? { scheduled.last }
}

@MainActor
@Suite("The rest alarm a workout warrants")
struct PendingRestAlarmTests {
    private static let now = AWorkout.start.addingTimeInterval(600)

    @Test("A running rest is an alarm at the instant it ends")
    func runningRestBecomesAnAlarm() {
        let began = Self.now.addingTimeInterval(-20)
        let alarm = PendingRestAlarm.of(
            AWorkout.live(rest: .running(since: began, duration: 120)),
            movementTitle: "Back Squat", at: Self.now)

        #expect(alarm?.endsAt == began.addingTimeInterval(120))
        #expect(alarm?.movementTitle == "Back Squat")
        #expect(alarm?.secondsRemaining(at: Self.now) == 100)
    }

    @Test("A workout written down afterwards still gets its alarm")
    func retroWorkoutStillAlarms() {
        // The distinction from the Live Activity, which a retro log deliberately has none of: the
        // logging surface is the same one, a retro workout can arm a rest, and an athlete who
        // armed it wants to hear it.
        let alarm = PendingRestAlarm.of(
            AWorkout.retro(rest: .running(since: Self.now, duration: 90)),
            movementTitle: "Bench Press", at: Self.now)

        #expect(alarm?.endsAt == Self.now.addingTimeInterval(90))
    }

    @Test("No workout, no rest, and a rest already over are all nothing to hold")
    func nothingWorthAnAlarm() {
        #expect(PendingRestAlarm.of(nil, movementTitle: nil, at: Self.now) == nil)
        #expect(PendingRestAlarm.of(
            AWorkout.live(rest: .idle), movementTitle: nil, at: Self.now) == nil)
        // A clock the store has not retired yet must not schedule an alarm in the past; a trigger
        // would refuse it and there is nothing left to announce.
        #expect(PendingRestAlarm.of(
            AWorkout.live(rest: .running(since: Self.now.addingTimeInterval(-120), duration: 60)),
            movementTitle: nil, at: Self.now) == nil)
    }

    @Test("The alert names the movement, and reads without one")
    func copyNamesTheMovement() {
        // A phone carrying three apps' timers answers "Rest over" with "which rest?".
        let named = PendingRestAlarm(endsAt: Self.now, movementTitle: "Back Squat")
        #expect(named.body.contains("Back Squat"))

        let unnamed = PendingRestAlarm(endsAt: Self.now, movementTitle: nil)
        #expect(!unnamed.body.isEmpty)
        #expect(unnamed.title == "Rest over")
    }

    @Test("An empty movement title is an absent one")
    func emptyTitleIsNil() {
        let alarm = PendingRestAlarm.of(
            AWorkout.live(rest: .running(since: Self.now, duration: 60)),
            movementTitle: "", at: Self.now)

        #expect(alarm?.movementTitle == nil)
    }
}

/// What the store leaves scheduled, and when.
@MainActor
@Suite("The rest alarm's lifecycle")
struct RestAlarmLifecycleTests {
    private static func aStore(
        _ centre: ANotificationCentre
    ) -> (ActiveWorkoutStore, ActiveWorkoutDocument) {
        let document = ActiveWorkoutDocument(
            directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
        let store = ActiveWorkoutStore(
            document: document,
            sessions: AnAgreeableServer(),
            restAlarm: centre,
            now: { AWorkout.start },
            movementTitle: { _ in "Back Squat" })
        return (store, document)
    }

    /// The mirror is an unstructured chain the store owns, so a test has to let it run.
    private static func settle() async {
        for _ in 0..<8 { await Task.yield() }
        try? await Task.sleep(for: .milliseconds(300))
    }

    private static func aStartedWorkout(
        _ centre: ANotificationCentre
    ) async -> ActiveWorkoutStore {
        let (store, _) = aStore(centre)
        store.begin(destination: .liveSession(AWorkout.session), startedAt: AWorkout.start)
        await settle()
        return store
    }

    @Test("Arming a rest leaves an alarm waiting")
    func armingSchedules() async {
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)

        store.startRest(90)
        await Self.settle()

        #expect(centre.pending??.endsAt == AWorkout.start.addingTimeInterval(90))
        #expect(centre.pending??.movementTitle == nil)  // No set logged, so no movement yet.
    }

    @Test("Skipping a rest takes the alarm down")
    func skippingWithdraws() async {
        // The failure this prevents is the loud one: a phone buzzing in a gym about a rest the
        // athlete cut short two minutes ago.
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()

        store.skipRest()
        await Self.settle()

        #expect(centre.pending == .some(nil))
    }

    @Test("Extending a rest moves the alarm rather than adding one")
    func extendingMovesIt() async {
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()

        store.extendRest(by: 30)
        await Self.settle()

        #expect(centre.pending??.endsAt == AWorkout.start.addingTimeInterval(120))
    }

    @Test("Finishing the workout takes the alarm down")
    func completingWithdraws() async {
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()

        #expect(await store.complete(effort: nil, endingAt: AWorkout.start.addingTimeInterval(3600)))
        await Self.settle()

        #expect(centre.pending == .some(nil))
    }

    @Test("Discarding the workout takes the alarm down")
    func discardingWithdraws() async {
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()

        await store.discard()
        await Self.settle()

        #expect(centre.pending == .some(nil))
    }

    @Test("Signing out takes down both the pending alarm and one already delivered")
    func signingOutForgetsEverything() async {
        // A delivered "Rest over — Back Squat" still names the last athlete's session, and
        // withdrawing the pending one cannot reach it.
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()

        store.reset()
        await Self.settle()

        #expect(centre.pending == .some(nil))
        #expect(centre.forgotDelivered == 1)
    }

    @Test("A rest survives a force-quit, and so does its alarm")
    func restoringReschedules() async {
        // The system holds the schedule and the document holds the end instant, so a launch into a
        // running rest has to put the alarm back rather than assume it is still there.
        let centre = ANotificationCentre()
        let (store, document) = Self.aStore(centre)
        document.write(AWorkout.live(rest: .running(since: AWorkout.start, duration: 120)))

        await store.restore()
        await Self.settle()

        #expect(centre.pending??.endsAt == AWorkout.start.addingTimeInterval(120))
    }

    @Test("A launch into a rest that already ended schedules nothing")
    func restoringAnExpiredRestWithdraws() async {
        let centre = ANotificationCentre()
        let (store, document) = Self.aStore(centre)
        document.write(AWorkout.live(
            rest: .running(since: AWorkout.start.addingTimeInterval(-600), duration: 60)))

        await store.restore()
        await Self.settle()

        #expect(centre.pending == .some(nil))
    }

    @Test("A change that is not about the rest does not move the alarm")
    func unrelatedMutationsLeaveItAlone() async {
        // The store mirrors on every mutation, so the alarm has to be derived rather than nudged:
        // recomputing the same instant is what lets the scheduler drop the duplicate instead of
        // re-triggering the buzz a little later each time a set is logged.
        let centre = ANotificationCentre()
        let store = await Self.aStartedWorkout(centre)
        store.startRest(90)
        await Self.settle()
        let armed = centre.pending

        store.note("felt strong")
        await Self.settle()

        #expect(centre.pending == armed)
    }
}
