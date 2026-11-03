import Foundation
import Observation

/// The in-flight workout, and the only thing that writes it to disk.
///
/// It is a store rather than a screen's state because the workout bar is visible from all five tabs
/// — there is no one screen that owns it. Every change persists before anything else happens, which
/// is the ordering that makes a force-quit survivable: if the app dies between the write and the
/// server push, the next launch finds the workout and the next push carries it.
///
/// Three rules it exists to keep, in the order they matter:
///
/// 1. **A set is on disk before it is on screen.** `adopt` writes, then publishes.
/// 2. **A failed push is a state, not an error.** The bar says so and the next push carries
///    everything; nothing is thrown at a view and nothing is dropped.
/// 3. **Nothing clears the document until another copy exists.** That is a 2xx on the completing
///    route, or the finished workout safely set down in `WorkoutOutbox` when the server cannot be
///    reached. Until one of those, the device's copy is the only one.
///
/// A workout started with no connection is a deferred start: it is logged exactly like any other,
/// and its session is opened on the server by the first push that gets through.
///
/// It owns exactly one workout. The server will happily hold several active sessions at once, so
/// the guard against starting a second is here — see `begin`.
@Observable
@MainActor
final class ActiveWorkoutStore {
    /// How long rapid logging is allowed to accumulate before a push.
    ///
    /// Long enough that a set and its correction are one request, short enough that putting the
    /// phone down after a set leaves it synced. Every push carries the whole list, so a longer
    /// window costs nothing but the time the device is the only copy.
    private static let coalescingWindow = Duration.milliseconds(900)

    /// How long a hand-over waits on the server before settling without it.
    ///
    /// The athlete is watching a spinner, and a Mac asleep behind Tailscale holds a request open
    /// for the system's whole minute rather than refusing it. Running out is safe because every
    /// step of a hand-over is safe to run again: a finish given up on goes to the outbox, whose
    /// delivery first asks what the server already holds. Both limits are `Deadline`'s, with what
    /// it does and does not promise.
    struct Patience: Equatable, Sendable {
        /// For a session opening already in flight, before it is cancelled. What a cancelled
        /// opening may have left on the server is found by the hand-over's own read.
        let opening: Duration
        /// For a finish or a discard.
        let handOver: Duration

        static let standard = Patience(opening: .seconds(5), handOver: .seconds(12))
    }

    /// The workout in flight, or nothing. `nil` is right here rather than a case: there is no third
    /// state, and the bar renders nothing for the absent one.
    private(set) var workout: ActiveWorkout?

    /// Whether the workout on screen is also the workout on disk.
    ///
    /// A separate fact from `ActiveWorkout.sync`, which is about the *server*. Both can fail
    /// independently and they want different words: unsynced means the next push will carry it,
    /// unpersisted means a force-quit would lose it.
    private(set) var isPersisted = true

    private(set) var finish: FinishState = .idle

    /// Where finishing a workout stands. `.refused` is the case that earns its keep: a workout
    /// whose sets have not reached the server must not be completed, and the athlete has to be told
    /// why rather than left with a button that does nothing.
    enum FinishState: Equatable, Sendable {
        case idle
        case finishing
        case refused(LoadFailure)
    }

    private let document: ActiveWorkoutDocument
    private let sessions: any WorkoutSessionWriting
    private let delivery: WorkoutDelivery
    private let outbox: WorkoutOutbox
    private let activity: any WorkoutActivityPresenting
    private let restAlarm: any RestAlarmScheduling
    private let now: @Sendable () -> Date
    private let onRestFinished: @MainActor () -> Void
    private let onServerUnanswered: @MainActor () -> Void
    private let patience: Patience
    private let movementTitle: @MainActor (UUID) -> String?

    private var pushHandle: Task<Void, Never>?
    private var restHandle: Task<Void, Never>?
    private var mirrorHandle: Task<Void, Never>?

    /// The request opening a deferred start's session while one is out, and the workout it is for.
    ///
    /// Deliberately not the push's own task. A push is cancelled and replaced every time a set is
    /// logged, but an opening request cancelled on the device may still have landed on the server,
    /// and a second one sent before the first's answer could be recognised would open a second
    /// session. So every push joins the one in flight, and a finish or a discard waits on it
    /// (`settleOpening`).
    private var opening: (localId: UUID, request: Task<UUID, any Error>)?

    /// Whether a finish or a discard is handing the workout over. No push starts meanwhile: the
    /// hand-over does its own writes, and a push opening a deferred start beside it would make a
    /// second session out of the same workout.
    private var isHandingOver = false

    /// - Parameter outbox: where a finished workout is set down when the server cannot be reached.
    /// - Parameter activity: the Lock Screen's copy of this workout. Defaults to one that shows
    ///   nothing, so a test exercises the store without another process in it.
    /// - Parameter restAlarm: what buzzes when the rest runs out, held by the system so it fires
    ///   with the app suspended. Defaults to one that never does, for the same reason.
    /// - Parameter onRestFinished: run when a rest runs out, for the haptic. Injected rather than
    ///   called directly so that nothing in this type reaches for UIKit, and because a rest that
    ///   ran out while the app was closed should not buzz on the next launch — only a rest this
    ///   store watched out announces itself.
    /// - Parameter movementTitle: turns the current movement's id into the name the Lock Screen
    ///   shows. Injected for the same reason the haptic is: the movement library is another store,
    ///   and this one should not reach for it.
    /// - Parameter onServerUnanswered: run when a hand-over settles without the server, so the
    ///   app starts asking after it. A finish that ran out of patience on a weak connection never
    ///   saw a request fail, and without this nothing would notice the server come back to take
    ///   the workout from the outbox.
    init(
        document: ActiveWorkoutDocument = ActiveWorkoutDocument(),
        sessions: any WorkoutSessionWriting,
        outbox: WorkoutOutbox,
        activity: any WorkoutActivityPresenting = NoWorkoutActivity(),
        restAlarm: any RestAlarmScheduling = NoRestAlarm(),
        now: @escaping @Sendable () -> Date = { Date() },
        onRestFinished: @escaping @MainActor () -> Void = {},
        movementTitle: @escaping @MainActor (UUID) -> String? = { _ in nil },
        onServerUnanswered: @escaping @MainActor () -> Void = {},
        patience: Patience = .standard
    ) {
        self.document = document
        self.sessions = sessions
        delivery = WorkoutDelivery(sessions: sessions)
        self.outbox = outbox
        self.activity = activity
        self.restAlarm = restAlarm
        self.now = now
        self.onRestFinished = onRestFinished
        self.movementTitle = movementTitle
        self.onServerUnanswered = onServerUnanswered
        self.patience = patience
    }

    // MARK: - Lifetime

    /// Reads whatever survived the last run. Called once, from the app's `.task`.
    ///
    /// Reading is off the main actor because it touches the file system, and the app's first frame
    /// should not wait on a disk read to draw. A workout found here is pushed at once: the last run
    /// may have ended between a set being logged and its push landing.
    func restore() async {
        workout = await Self.read(from: document)
        isPersisted = true
        // Reconciles whatever ActivityKit kept alive across the launch, and runs even when there
        // is no document — that empty case is what clears an activity left behind by a workout
        // completed or discarded while the app was not running.
        mirror()
        armRestTimer()
        if workout?.sync.isWorthPushing == true { schedulePush() }
    }

    /// Begins a workout. Refuses while one is already in flight.
    ///
    /// The refusal is the device's, not the server's: `POST /active` would create a second session
    /// quite happily, and the device can only hold one, so the extra one would be an orphan nobody
    /// could reach. Callers show "continue" rather than "start" instead of calling this.
    ///
    /// - Parameter plannedSessionId: the planned session this workout is the training of, if any.
    /// - Parameter routine: the routine it was started from, if any. Its movements are added in
    ///   order, so each has a section to log its first set in.
    /// - Returns: whether the workout began.
    @discardableResult
    func begin(
        destination: WorkoutDestination,
        startedAt: Date,
        sets: [LoggedSet] = [],
        prescriptions: [SetPrescription] = [],
        notes: String = "",
        plannedSessionId: UUID? = nil,
        routine: WorkoutRoutine? = nil
    ) -> Bool {
        guard workout == nil else { return false }

        adopt(ActiveWorkout(
            destination: destination,
            startedAt: startedAt,
            sync: Self.syncAtStart(of: destination),
            sets: sets,
            prescriptions: prescriptions,
            addedMovementDataIds: routine?.movements.map(\.movementDataId) ?? [],
            notes: notes,
            plannedSessionId: plannedSessionId,
            routine: routine))
        finish = .idle
        // A deferred start has a session to open, and trying straight away costs nothing: the
        // server may well be back by the time the athlete has picked a movement.
        if case .deferredStart = destination { schedulePush() }
        return true
    }

    /// Resumed sets are already on the server, and a workout that begins with none has nothing to
    /// push — except a deferred start, whose session the server does not have yet.
    private static func syncAtStart(of destination: WorkoutDestination) -> WorkoutSyncState {
        switch destination {
        case .liveSession, .afterTheFact: .synced
        case .deferredStart: .pending
        }
    }

    /// Pushes now if anything is waiting to reach the server. Called when the server is reachable
    /// again, which is the moment a deferred start can be opened and a failed push can land.
    func serverReturned() async {
        guard workout?.sync.isWorthPushing == true else { return }
        await syncNow()
    }

    /// Empties the store. Registered with `AuthStore`, so signing out cannot leave one athlete's
    /// workout on screen for the next one.
    ///
    /// The document goes too. A workout belongs to the session the signed-out credential owned, and
    /// a document the next sign-in could adopt would push one athlete's sets at another athlete's
    /// session id — which is a worse failure than losing a workout to a deliberate sign-out.
    func reset() {
        pushHandle?.cancel()
        pushHandle = nil
        // The one place an opening is cancelled: the credential it was sent with is gone, and the
        // next athlete's workout must not wait on, or adopt, this one's session.
        opening?.request.cancel()
        opening = nil
        restHandle?.cancel()
        restHandle = nil
        workout = nil
        isPersisted = true
        finish = .idle
        document.clear()
        // An activity surviving a sign-out would leave the last athlete's session counting down on
        // the Lock Screen of a signed-out phone, which is the one leak this teardown exists to
        // prevent.
        mirror()

        // A rest alert that already *fired* is still sitting in Notification Centre naming their
        // last movement, which `mirror` cannot reach — it withdraws what is pending, and this one
        // has been delivered. Only sign-out does this: after an ordinary rest the athlete saw the
        // alert, and pulling it out from under them when they open the app would be worse.
        let scheduling = restAlarm
        inOrder { await scheduling.forgetDelivered() }
    }

    // MARK: - Logging

    /// Logs `command` as the next set, and starts a rest if one was asked for.
    ///
    /// - Parameter restingFor: the rest to start, or `nil` to leave the clock alone. The duration
    ///   is the view's to decide because it is the athlete's preference, which is view state.
    func log(_ command: SetCommand, restingFor rest: TimeInterval?) {
        guard let workout else { return }
        adopt(workout.appending(LoggedSet(loggedAt: now(), command: command), restingFor: rest))
        armRestTimer()
        schedulePush()
    }

    /// Corrects a set already logged. A typo in a weight is the common case.
    func restate(_ id: UUID, as command: SetCommand) {
        guard let workout else { return }
        adopt(workout.restating(id, as: command))
        schedulePush()
    }

    func remove(_ id: UUID) {
        guard let workout else { return }
        adopt(workout.removing(id))
        schedulePush()
    }

    /// Adds a movement to the workout and moves the athlete onto it — what picking one does.
    ///
    /// Persisted like a set, because it is what the athlete is about to log against: a force-quit
    /// between picking a lift and logging it must not leave them hunting for it again. Nothing is
    /// pushed, since the server's set list has not changed.
    func add(_ movementDataId: UUID) {
        guard let workout else { return }
        adopt(workout.adding(movementDataId))
    }

    /// Takes back a movement added by mistake. A movement with a set on it stays; see
    /// `ActiveWorkout.dropping(_:)`.
    func drop(_ movementDataId: UUID) {
        guard let workout else { return }
        let dropped = workout.dropping(movementDataId)
        guard dropped != workout else { return }
        adopt(dropped)
    }

    func work(on movementDataId: UUID?) {
        guard let workout, workout.currentMovementDataId != movementDataId else { return }
        adopt(workout.working(on: movementDataId))
    }

    func note(_ notes: String) {
        guard let workout, workout.notes != notes else { return }
        adopt(workout.noting(notes))
    }

    /// Moves the start of a workout being written down afterwards.
    ///
    /// Refused for a live session, whose start is the server's: the period sent to `/complete` is
    /// built from it, and a device-side edit would complete the session over a window it did not
    /// run in.
    func moveStart(to startedAt: Date) {
        guard let workout, case .afterTheFact = workout.destination,
              workout.startedAt != startedAt
        else { return }
        adopt(workout.starting(at: startedAt))
    }

    // MARK: - The rest clock

    func startRest(_ duration: TimeInterval) {
        guard let workout else { return }
        adopt(workout.resting(.running(since: now(), duration: duration)))
        armRestTimer()
    }

    /// Lengthens the rest rather than restarting it — the athlete wants thirty seconds more, not
    /// a fresh two minutes.
    func extendRest(by extra: TimeInterval) {
        guard let workout, case .running = workout.rest else { return }
        adopt(workout.resting(workout.rest.extended(by: extra)))
        armRestTimer()
    }

    func skipRest() {
        guard let workout, case .running = workout.rest else { return }
        restHandle?.cancel()
        restHandle = nil
        adopt(workout.resting(.idle))
    }

    /// Arms a single sleep for whatever is left of the rest.
    ///
    /// A stored `Task` rather than a `Timer`: it is cancelled and replaced on every change to the
    /// clock, it cannot outlive the store, and it costs one wakeup instead of one a second. The
    /// *view* ticks through `TimelineView(.periodic)`, so the seconds on screen redraw the clock
    /// and not the set list above it.
    private func armRestTimer() {
        restHandle?.cancel()
        restHandle = nil

        guard let workout, case .running = workout.rest else { return }
        let remaining = workout.rest.remaining(at: now())

        // A rest that ran out while the app was away is tidied up without announcing itself: the
        // athlete has been back for some time and a buzz now would be about nothing.
        guard remaining > 0 else { retireRest(announcing: false); return }

        restHandle = Task { [weak self] in
            do {
                try await Task.sleep(for: .seconds(remaining))
            } catch {
                return  // Cancelled: the clock was changed or the store was reset.
            }
            self?.retireRest(announcing: true)
        }
    }

    /// Puts the clock away once the rest is over, so the bar stops saying the athlete is resting.
    private func retireRest(announcing: Bool) {
        restHandle = nil
        guard let workout, case .running = workout.rest else { return }
        adopt(workout.resting(.idle))
        if announcing { onRestFinished() }
    }

    // MARK: - Reaching the server

    /// Pushes after the coalescing window, replacing any push already waiting.
    ///
    /// Cancel-and-replace is what makes five sets logged in ten seconds one request: each new set
    /// pushes the deadline out, and every push carries the whole list anyway, so the one that
    /// eventually runs is never behind.
    private func schedulePush() {
        guard workout?.destination.pushesAsItGoes == true, !isHandingOver else { return }

        pushHandle?.cancel()
        pushHandle = Task { [weak self] in
            do {
                try await Task.sleep(for: Self.coalescingWindow)
            } catch {
                return
            }
            await self?.push()
        }
    }

    /// Sends the whole set list now, for the bar's "try again".
    func syncNow() async {
        pushHandle?.cancel()
        pushHandle = nil
        await push()
    }

    private func push() async {
        guard let workout, !isHandingOver else { return }

        let sessionId: UUID
        switch workout.destination {
        case .afterTheFact:
            return
        case .liveSession(let id):
            sessionId = id
        case .deferredStart(let localId):
            do {
                sessionId = try await openSession(for: workout, localId: localId)
            } catch is CancellationError {
                return
            } catch {
                mark(refusalOrFailure(from: error))
                return
            }
        }

        // Read again rather than reusing the value above: opening a session is a round trip, and
        // a set logged during it belongs in this push. Only while it is still this session's
        // workout and nothing is handing it over.
        guard let current = self.workout, current.destination.sessionId == sessionId, !isHandingOver
        else { return }
        let pushed = current.sets.map(\.command)

        do {
            _ = try await sessions.replaceSets(id: sessionId, with: pushed)
        } catch is CancellationError {
            return
        } catch {
            mark(refusalOrFailure(from: error))
            return
        }

        // Only claim synced if nothing was logged while the request was out. A set that arrived
        // mid-flight has not been sent, and saying otherwise is the one lie this type must not
        // tell.
        guard self.workout?.sets.map(\.command) == pushed else {
            mark(.pending)
            return
        }
        mark(.synced)
    }

    /// The server's session for a deferred start, opened if it is not open yet.
    ///
    /// The opening joins one already in flight rather than sending another (see `opening`), and the
    /// workout is moved onto the session the moment it exists, so a force-quit after this point
    /// relaunches into an ordinary live workout.
    private func openSession(for workout: ActiveWorkout, localId: UUID) async throws -> UUID {
        let request = opening.flatMap { $0.localId == localId ? $0.request : nil }
            ?? Task { [delivery] in try await delivery.open(workout) }
        opening = (localId, request)
        // Released however it ends: an opening that failed must not answer the next attempt too.
        defer { if opening?.request == request { opening = nil } }
        let sessionId = try await request.value
        adoptOpened(sessionId, for: localId)
        return sessionId
    }

    /// Moves the deferred start `localId` onto the session the server opened for it — and nothing
    /// else: an opening that lands after its workout was discarded must not claim the next one.
    private func adoptOpened(_ sessionId: UUID, for localId: UUID) {
        guard let current = workout, current.destination == .deferredStart(localId: localId) else { return }
        adopt(current.opened(as: sessionId))
    }

    /// Waits for an opening in flight to land, cancelling it once `patience.opening` has passed.
    private func settleOpening() async {
        guard let inFlight = opening else { return }
        let request = inFlight.request
        let landed = try? await Deadline.waiting(
            atMost: patience.opening, expiring: { CancellationError() }
        ) {
            // Awaiting a task's value does not end when the waiter is cancelled; cancelling the
            // request does, which is what lets the deadline win.
            try await withTaskCancellationHandler {
                try await request.value
            } onCancel: {
                request.cancel()
            }
        }
        if let landed { adoptOpened(landed, for: inFlight.localId) }
    }

    /// What finishing came to.
    enum FinishOutcome: Equatable, Sendable {
        /// The server has the workout.
        case delivered
        /// The server could not be reached, so the workout was set down in the outbox and will be
        /// delivered when it can be.
        case heldForDelivery
        /// Not finished. `finish` says why, and the workout is still in flight.
        case notFinished
    }

    /// Finishes the workout and hands it to the server, or, when the server cannot be reached, to
    /// the outbox that will.
    ///
    /// The transaction is `WorkoutDelivery.deliver`'s: a live session has its sets pushed before it
    /// is completed, a workout logged after the fact is one `POST /completed`, and a deferred start
    /// is whichever of those the server's state calls for. What this method adds is the device's
    /// side: nothing is cleared until the server has the workout or the outbox holds it on disk.
    @discardableResult
    func complete(effort: PerceivedEffortRequest?, endingAt end: Date) async -> FinishOutcome {
        guard workout != nil, !isHandingOver else { return .notFinished }
        finish = .finishing
        isHandingOver = true
        defer { isHandingOver = false }

        // A coalescing push still waiting would otherwise race this one and land after the session
        // was completed, where it can only be refused.
        pushHandle?.cancel()
        pushHandle = nil
        // An opening in flight lands first, so the finish reads a server that already shows it
        // rather than opening the same workout a second time.
        await settleOpening()
        guard let workout else { return .notFinished }

        do {
            try await Deadline.waiting(atMost: patience.handOver, expiring: Self.outOfPatience) {
                [delivery] in
                try await delivery.deliver(workout, endingAt: end, effort: effort, mayHaveLanded: false)
            }
        } catch is CancellationError {
            finish = .idle
            return .notFinished
        } catch {
            // Signed out while the request was out: the workout went with the account, and setting
            // it down now would hand it to whoever signs in next.
            guard self.workout != nil else { return .notFinished }
            let failure = (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))
            if case .offline = failure {
                onServerUnanswered()
                return holdForDelivery(FinishedWorkout(workout: workout, endedAt: end, effort: effort))
            }
            finish = .refused(failure)
            if case .refused = failure { mark(.refused(failure.message)) }
            return .notFinished
        }

        clearLocally()
        return .delivered
    }

    /// Sets the finished workout down in the outbox and lets it go from here, in that order.
    private func holdForDelivery(_ finished: FinishedWorkout) -> FinishOutcome {
        guard outbox.hold(finished) else {
            finish = .refused(.unavailable(
                "Lionheart is not reachable, and the finished workout could not be saved on this phone."))
            return .notFinished
        }
        clearLocally()
        return .heldForDelivery
    }

    /// Abandons the workout, and removes the session it was holding.
    ///
    /// The delete is best-effort and the local clear is not. A session left active on the server is
    /// visible in the Train tab and can be discarded again from there; a workout the athlete cannot
    /// dismiss is a dead end, and an accidental tap on "start" must not be one. A session the
    /// server has completed is never deleted; see `WorkoutDelivery.withdraw`.
    func discard() async {
        guard let workout, !isHandingOver else { return }
        isHandingOver = true
        defer { isHandingOver = false }
        pushHandle?.cancel()
        pushHandle = nil
        await settleOpening()

        let withdrawn = self.workout ?? workout
        do {
            try await Deadline.waiting(atMost: patience.handOver, expiring: Self.outOfPatience) {
                [delivery] in
                await delivery.withdraw(withdrawn)
            }
        } catch {
            // The withdrawal itself never throws; only running out of patience does.
            onServerUnanswered()
        }
        clearLocally()
    }

    /// A hand-over that ran out of patience reads as a server that could not be reached, which
    /// from the gym floor it is.
    private nonisolated static func outOfPatience() -> any Error {
        APIError.transport("The server did not answer in time.")
    }

    // MARK: -

    /// Drops the local copy of the workout. It must never be called before the server or the outbox
    /// holds the workout (or the athlete has asked to throw it away): this is what makes the
    /// device's copy unrecoverable.
    private func clearLocally() {
        restHandle?.cancel()
        restHandle = nil
        workout = nil
        isPersisted = true
        finish = .idle
        document.clear()
        // Completed and discarded both arrive here, and both have to take the Lock Screen's copy
        // with them: an activity outliving a discarded workout counts down a rest that will never
        // end.
        mirror()
    }

    private func mark(_ sync: WorkoutSyncState) {
        guard let workout, workout.sync != sync else { return }
        adopt(workout.marking(sync))
    }

    /// Restates a network failure as a sync state, once, here.
    ///
    /// A 409 or a 404 means the server will not take these sets — the session was completed or
    /// deleted somewhere else — and no number of retries changes that, so it is `.refused` and the
    /// bar says so. Everything else is worth another push.
    private func refusalOrFailure(from error: any Error) -> WorkoutSyncState {
        guard let failure = try? LoadFailure.from(error) else {
            return .failed(String(describing: error))
        }
        switch failure {
        case .refused(let reason):
            return .refused(reason)
        case .notFound:
            return .refused("This session is no longer on the server.")
        case .offline, .unavailable:
            return .failed(failure.message)
        }
    }

    /// Writes the value, then publishes it.
    ///
    /// The order is the whole point and is easy to reverse by accident: publishing first would
    /// render a workout that is not yet on disk, so a crash in between would lose something the
    /// athlete had already seen confirmed. The Lock Screen comes last of the three for the same
    /// reason it comes last everywhere: it is a mirror, and a mirror of something not yet saved is
    /// the one ordering that could show the athlete a set that is about to be lost.
    private func adopt(_ next: ActiveWorkout) {
        isPersisted = document.write(next)
        workout = next
        mirror()
    }

    // MARK: - The copies outside this store

    /// Hands the workout to everything outside this store that holds a copy of it, in order.
    ///
    /// There are two: the Live Activity, drawn by another process, and the alert the system holds
    /// to fire when the rest runs out. Both are second representations of the same state and both
    /// have the same failure — one left behind, counting down or buzzing about a rest the athlete
    /// skipped — so both go through this one call rather than through schedule-and-cancel pairs
    /// scattered over the store. Every way a workout can change already funnels into `adopt`,
    /// `restore`, `clearLocally` and `reset`, which is what makes a single call here sufficient.
    ///
    /// Each call waits on the one before it, because two mutations a few milliseconds apart could
    /// otherwise land in the other order — leaving a countdown on screen, or an alarm in the
    /// system, for a rest that is already over. Both answers are derived synchronously so each
    /// link carries the state it was made for.
    private func mirror() {
        let title = workout?.currentMovementDataId.flatMap { movementTitle($0) }
        let moment = now()

        let projection = WorkoutActivityProjection.of(
            workout, movementTitle: title, isPersisted: isPersisted, at: moment)
        let alarm = PendingRestAlarm.of(workout, movementTitle: title, at: moment)

        let presenting = activity
        let scheduling = restAlarm
        inOrder {
            await presenting.present(projection)
            await scheduling.schedule(alarm)
        }
    }

    /// Runs `step` after everything already handed to the copies outside this store.
    ///
    /// The chain itself, in one place because two callers need it. It is never cancelled: the last
    /// thing a sign-out enqueues is the instruction to take those copies down, and cancelling here
    /// is how an orphaned activity or a stray alarm would survive one.
    private func inOrder(_ step: @escaping @MainActor () async -> Void) {
        let previous = mirrorHandle
        mirrorHandle = Task {
            await previous?.value
            await step()
        }
    }

    /// Reading the file off the main actor. `@concurrent` states the offload rather than relying on
    /// the current default for a nonisolated `async` function.
    @concurrent
    private nonisolated static func read(from document: ActiveWorkoutDocument) async -> ActiveWorkout? {
        document.read()
    }
}
