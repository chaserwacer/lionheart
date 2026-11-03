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
/// 3. **Only a 2xx on the completing route clears the document.** Until the server has the session,
///    the device is the only copy.
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
    private let now: @Sendable () -> Date
    private let onRestFinished: @MainActor () -> Void

    private var pushHandle: Task<Void, Never>?
    private var restHandle: Task<Void, Never>?

    /// - Parameter onRestFinished: run when a rest runs out, for the haptic. Injected rather than
    ///   called directly so that nothing in this type reaches for UIKit, and because a rest that
    ///   ran out while the app was closed should not buzz on the next launch — only a rest this
    ///   store watched out announces itself.
    init(
        document: ActiveWorkoutDocument = ActiveWorkoutDocument(),
        sessions: any WorkoutSessionWriting,
        now: @escaping @Sendable () -> Date = { Date() },
        onRestFinished: @escaping @MainActor () -> Void = {}
    ) {
        self.document = document
        self.sessions = sessions
        self.now = now
        self.onRestFinished = onRestFinished
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
        armRestTimer()
        if workout?.sync.isWorthPushing == true { schedulePush() }
    }

    /// Begins a workout. Refuses while one is already in flight.
    ///
    /// The refusal is the device's, not the server's: `POST /active` would create a second session
    /// quite happily, and the device can only hold one, so the extra one would be an orphan nobody
    /// could reach. Callers show "continue" rather than "start" instead of calling this.
    ///
    /// - Returns: whether the workout began.
    @discardableResult
    func begin(
        destination: WorkoutDestination,
        startedAt: Date,
        sets: [LoggedSet] = [],
        prescriptions: [SetPrescription] = [],
        notes: String = ""
    ) -> Bool {
        guard workout == nil else { return false }

        adopt(ActiveWorkout(
            destination: destination,
            startedAt: startedAt,
            // Resumed sets are already on the server; a workout that begins with none has nothing
            // to push either way.
            sync: .synced,
            sets: sets,
            prescriptions: prescriptions,
            notes: notes))
        finish = .idle
        return true
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
        restHandle?.cancel()
        restHandle = nil
        workout = nil
        isPersisted = true
        finish = .idle
        document.clear()
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
        guard workout?.destination.pushesAsItGoes == true else { return }

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
        guard let workout, let sessionId = workout.destination.sessionId else { return }
        let pushed = workout.sets.map(\.command)

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

    /// Finishes the workout and hands it to the server.
    ///
    /// Two different transactions, which is what `WorkoutDestination` exists to distinguish. A live
    /// session has to have its sets pushed **first** — `POST /{id}/complete` carries only the
    /// period and the effort, so completing over an unsynced set list would complete the session
    /// without it. A workout logged after the fact is one `POST /completed` carrying everything.
    ///
    /// - Returns: whether the server took it. Only then is the local document cleared.
    @discardableResult
    func complete(effort: PerceivedEffortRequest?, endingAt end: Date) async -> Bool {
        guard let workout else { return false }
        finish = .finishing

        // A coalescing push still waiting would otherwise race this one and land after the session
        // was completed, where it can only be refused.
        pushHandle?.cancel()
        pushHandle = nil

        do {
            switch workout.destination {
            case .liveSession(let sessionId):
                _ = try await sessions.replaceSets(id: sessionId, with: workout.sets.map(\.command))
                if !workout.notes.isEmpty {
                    // Notes ride their own route in every lifecycle state — they are commentary,
                    // not state — so they go before the transition rather than with it. In the
                    // chain rather than best-effort: silently dropping what the athlete wrote about
                    // the session is worse than a refusal they can act on.
                    _ = try await sessions.updateNotes(id: sessionId, to: workout.notes)
                }
                _ = try await sessions.complete(
                    id: sessionId,
                    period: .interval(from: workout.startedAt, to: end),
                    effort: effort?.isEmpty == true ? nil : effort)

            case .afterTheFact:
                _ = try await sessions.logCompleted(
                    sets: workout.sets.map(\.command),
                    period: .interval(from: workout.startedAt, to: end),
                    effort: effort?.isEmpty == true ? nil : effort,
                    notes: workout.notes,
                    programId: nil)
            }
        } catch is CancellationError {
            finish = .idle
            return false
        } catch {
            let failure = (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))
            finish = .refused(failure)
            if case .refused = failure { mark(.refused(failure.message)) }
            return false
        }

        clearLocally()
        return true
    }

    /// Abandons the workout, and removes the session it was holding.
    ///
    /// The delete is best-effort and the local clear is not. A session left active on the server is
    /// visible in the Train tab and can be discarded again from there; a workout the athlete cannot
    /// dismiss is a dead end, and an accidental tap on "start" must not be one.
    func discard() async {
        guard let workout else { return }
        pushHandle?.cancel()
        pushHandle = nil

        if let sessionId = workout.destination.sessionId {
            try? await sessions.delete(id: sessionId)
        }
        clearLocally()
    }

    // MARK: -

    /// Drops the local copy of the workout. The one thing it must never be called before is a 2xx
    /// from the completing route — this is what makes the device's copy unrecoverable.
    private func clearLocally() {
        restHandle?.cancel()
        restHandle = nil
        workout = nil
        isPersisted = true
        finish = .idle
        document.clear()
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
    /// athlete had already seen confirmed.
    private func adopt(_ next: ActiveWorkout) {
        isPersisted = document.write(next)
        workout = next
    }

    /// Reading the file off the main actor. `@concurrent` states the offload rather than relying on
    /// the current default for a nonisolated `async` function.
    @concurrent
    private nonisolated static func read(from document: ActiveWorkoutDocument) async -> ActiveWorkout? {
        document.read()
    }
}
