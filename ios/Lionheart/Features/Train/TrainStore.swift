import Foundation
import Observation

/// The Train tab's data, and the three ways a workout begins.
///
/// Starting is here rather than in the view because each way is a server transition followed by the
/// device adopting its answer, and those two steps must not come apart: a session started on the
/// server that the device does not adopt is an orphan nobody can reach, so the adoption happens in
/// the same method, immediately, against the id the server just gave back.
///
/// Everything the tab decides is `TrainingPlan.reading`; this type does IO and turns each outcome
/// into one state case.
@Observable
@MainActor
final class TrainStore {
    /// How wide a window of sessions the tab reads.
    ///
    /// Further forward than Today's, because planning is what this tab is for, and far enough back
    /// to show a session missed last week — starting one late is normal and it should not have to
    /// be hunted for in History.
    private static let lookBack: TimeInterval = 30 * 86_400
    private static let lookAhead: TimeInterval = 90 * 86_400

    private(set) var state: LoadState<TrainingPlan> = .loading

    /// Where the transition the athlete last asked for stands — starting, skipping or deleting a
    /// session.
    ///
    /// `.refused` carries the server's own words: a 409 here means the session is not in the state
    /// the transition needs — most often because it was started or skipped somewhere else — and the
    /// athlete is told rather than left looking at a row that did nothing.
    private(set) var action: ActionState = .idle

    private let sessions: TrainingSessionsAPI
    private let activeWorkout: ActiveWorkoutStore
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?

    init(
        sessions: TrainingSessionsAPI,
        activeWorkout: ActiveWorkoutStore,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.sessions = sessions
        self.activeWorkout = activeWorkout
        self.now = now
    }

    /// Whether a workout is already in flight on this device. The tab offers "continue" rather
    /// than "start" when it is, because the device holds one workout and the server would
    /// cheerfully create a second session it could not reach.
    var isWorkoutInFlight: Bool { activeWorkout.workout != nil }

    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        action = .idle
    }

    func dismissRefusal() {
        action = .idle
    }

    private func reload() async {
        let moment = now()
        do {
            let window = try await sessions.sessions(
                from: moment.addingTimeInterval(-Self.lookBack),
                to: moment.addingTimeInterval(Self.lookAhead))
            state = state.applying(.success(
                TrainingPlan.reading(sessions: window, today: CalendarDay(moment))))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    // MARK: - Starting a workout

    /// Planned → Active, then the device adopts it with the plan's targets in hand.
    ///
    /// The prescriptions come from the planned session the athlete tapped rather than from the
    /// server's answer, because the answer is the *active* session and an active session carries no
    /// prescriptions — the lifecycle moved them out of scope. They are carried into the document so
    /// the targets are on screen with no signal.
    @discardableResult
    func start(planned session: TrainingSession.Planned) async -> Bool {
        guard !isWorkoutInFlight else { return false }
        action = .running

        do {
            let started = try await sessions.start(
                id: session.envelope.trainingSessionId, at: now())
            guard let active = started.activeSession else {
                // The route answered 2xx with something that is not an active session. Adopting it
                // would push sets at a session that will refuse them.
                action = .refused(.unavailable("That session did not become an active one."))
                return false
            }
            adopt(
                destination: .liveSession(active.envelope.trainingSessionId),
                startedAt: active.startedAt,
                prescriptions: session.prescriptions,
                notes: active.envelope.notes)
            return true
        } catch {
            action = ActionState.refusal(from: error)
            return false
        }
    }

    /// An unplanned start — the athlete simply began training.
    @discardableResult
    func startUnplanned() async -> Bool {
        guard !isWorkoutInFlight else { return false }
        action = .running

        do {
            let started = try await sessions.startUnplanned(at: now())
            guard let active = started.activeSession else {
                action = .refused(.unavailable("That session did not become an active one."))
                return false
            }
            adopt(
                destination: .liveSession(active.envelope.trainingSessionId),
                startedAt: active.startedAt,
                prescriptions: [],
                notes: active.envelope.notes)
            return true
        } catch {
            action = ActionState.refusal(from: error)
            return false
        }
    }

    /// Planned → Skipped. Terminal, and the session is kept: the skip is data.
    func skip(_ session: TrainingSession.Planned) async {
        await write { _ = try await self.sessions.skip(id: session.envelope.trainingSessionId) }
    }

    /// Removes a planned session entirely — the remedy for one planned by mistake, or for one the
    /// athlete will not do and does not want kept as a skip, which a skip would be.
    func delete(_ session: TrainingSession.Planned) async {
        await write { try await self.sessions.delete(id: session.envelope.trainingSessionId) }
    }

    /// One transition that does not hand this device a workout, then a re-read of the window.
    ///
    /// Starting is not one of these: it ends by adopting the session into `ActiveWorkoutStore` and
    /// answers whether it got that far, so it keeps its own body.
    private func write(_ transition: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running

        do {
            try await transition()
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
        }
    }

    /// Begins a workout that already happened. Nothing is sent: `POST /completed` carries the whole
    /// thing when the athlete finishes, which is why this one works with no connection at all.
    @discardableResult
    func beginAfterTheFact(startedAt: Date) -> Bool {
        guard !isWorkoutInFlight else { return false }
        action = .idle
        return activeWorkout.begin(destination: .afterTheFact, startedAt: startedAt)
    }

    /// Takes over an active session the server holds but this device has no document for — one
    /// started on the web app, or left behind by a reinstall.
    ///
    /// Refused outright when the session carries a set shape this build cannot write back, because
    /// `PUT /sets` replaces the whole list and the next set logged would delete it. Losing the
    /// athlete's work by carrying on is worse than not carrying on.
    @discardableResult
    func resume(_ session: TrainingSession.Active) -> Bool {
        guard !isWorkoutInFlight else { return false }

        do {
            let sets = try WorkoutResumption.sets(of: session, loggedAt: session.startedAt)
            action = .idle
            return activeWorkout.begin(
                destination: .liveSession(session.envelope.trainingSessionId),
                startedAt: session.startedAt,
                sets: sets,
                notes: session.envelope.notes)
        } catch {
            switch error {
            case .cannotRestate(let type):
                action = .refused(.refused(
                    "This session has a set of a kind this version cannot edit (\(type)). "
                        + "Finish it on the web app so nothing is lost."))
            }
            return false
        }
    }

    private func adopt(
        destination: WorkoutDestination,
        startedAt: Date,
        prescriptions: [SetPrescription],
        notes: String
    ) {
        activeWorkout.begin(
            destination: destination,
            startedAt: startedAt,
            prescriptions: prescriptions,
            notes: notes)
        action = .idle
    }
}
