import Foundation

/// The server's half of a workout: opening its session, and handing it over once it is finished.
///
/// Both are written to be safe to run again, because both are run again — after a connection that
/// dropped mid-request, after a relaunch, every time the server comes back. So each one first asks
/// what the server already holds of the workout (`ServerCopy`) wherever an earlier attempt could
/// have landed without its answer, and only then writes. Sending blindly would make a second
/// session out of the same sets.
struct WorkoutDelivery: Sendable {
    let sessions: any WorkoutSessionWriting

    /// How far either side of a workout's start the server is asked about when looking for it.
    /// Wide enough to be indifferent to clock skew between the phone and the server, narrow enough
    /// that the read is a handful of rows.
    static let searchMargin: TimeInterval = 60

    // MARK: - Opening

    /// Opens the session a deferred start is waiting for, and answers its id.
    ///
    /// The plan it was started from is started if it is still planned. If it is not, because it was
    /// started, skipped or removed somewhere else, the workout opens an unplanned session of its
    /// own: the sets are real whatever became of the plan, and refusing them would lose them.
    func open(_ workout: ActiveWorkout) async throws -> UUID {
        switch try await copy(of: workout) {
        case .active(let id):
            return id
        case .planWaiting(let plan):
            switch try await start(plan, for: workout) {
            case .active(let id): return id
            case .completed: throw Self.alreadyCompleted
            case .absent, .planWaiting: return try await openUnplanned(workout)
            }
        case .absent:
            return try await openUnplanned(workout)
        case .completed:
            throw Self.alreadyCompleted
        }
    }

    private static let alreadyCompleted = APIError.refused(detail: "This workout is already recorded as completed.")

    /// Starts the plan a workout came from, and answers what the plan holds of the workout after:
    /// `.active` with the session it became, `.completed` if the workout is already on it, or
    /// `.absent` when it is no longer this workout's to start.
    ///
    /// A refusal is read back before it is believed. The plan may have been started or skipped
    /// elsewhere, but the 409 may equally be this device's own earlier start, landed after the
    /// device stopped waiting for its answer, and only the plan's start can tell the two apart.
    private func start(_ plan: UUID, for workout: ActiveWorkout) async throws -> ServerCopy {
        do {
            return .active(try Self.activeId(await sessions.start(id: plan, at: workout.startedAt)))
        } catch let refusal as APIError where refusal.losesThePlan {
            return ServerCopy.ofPlan(try await session(plan), of: workout) ?? .absent
        }
    }

    private func openUnplanned(_ workout: ActiveWorkout) async throws -> UUID {
        try Self.activeId(await sessions.startUnplanned(
            at: workout.startedAt, notes: workout.notes, programId: nil))
    }

    // MARK: - Delivering

    /// Hands a finished workout to the server. Running it for a workout already delivered does
    /// nothing.
    ///
    /// - Parameter mayHaveLanded: whether an earlier attempt could have reached the server without
    ///   its answer. A workout logged afterwards has no id to ask after, so only then is the server
    ///   searched before `POST /completed` is sent again; a first attempt goes straight out.
    func deliver(
        _ workout: ActiveWorkout,
        endingAt end: Date,
        effort: PerceivedEffortRequest?,
        mayHaveLanded: Bool
    ) async throws {
        let period = PeriodRequest.interval(from: workout.startedAt, to: end)
        let effort = effort?.isEmpty == true ? nil : effort

        switch workout.destination {
        case .liveSession(let id):
            try await complete(id, workout, period: period, effort: effort)

        case .afterTheFact:
            if mayHaveLanded, case .completed = try await nearby(workout) { return }
            try await logWhole(workout, period: period, effort: effort)

        case .deferredStart:
            switch try await copy(of: workout) {
            case .completed:
                return
            case .active(let id):
                try await complete(id, workout, period: period, effort: effort)
            case .planWaiting(let plan):
                switch try await start(plan, for: workout) {
                case .active(let id): try await complete(id, workout, period: period, effort: effort)
                case .completed: return
                case .absent, .planWaiting: try await logWhole(workout, period: period, effort: effort)
                }
            case .absent:
                // Never reached the server while it was being trained, so it lands the way a
                // workout written down afterwards does: whole, in one request.
                try await logWhole(workout, period: period, effort: effort)
            }
        }
    }

    /// Pushes the sets, then the notes, then completes. The sets go first because
    /// `POST /{id}/complete` carries only the period and the effort, so completing over an unsynced
    /// list would complete the session without them.
    ///
    /// A failure is not the end of it. If the server committed the completion and only the answer
    /// was lost, the session reads back completed, and that is a delivery. If the session is gone,
    /// deleted somewhere else, the workout lands whole as a session of its own: the sets are real
    /// whatever became of the session they were logged against.
    private func complete(
        _ id: UUID, _ workout: ActiveWorkout, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws {
        do {
            _ = try await sessions.replaceSets(id: id, with: workout.sets.map(\.command))
            if !workout.notes.isEmpty {
                // Notes ride their own route in every lifecycle state, so they go before the
                // transition rather than with it. In the chain rather than best-effort: silently
                // dropping what the athlete wrote is worse than a refusal they can act on.
                _ = try await sessions.updateNotes(id: id, to: workout.notes)
            }
            _ = try await sessions.complete(id: id, period: period, effort: effort)
        } catch is CancellationError {
            throw CancellationError()
        } catch let failure {
            // A read that itself fails answers "not delivered", which keeps the device's copy: the
            // recoverable mistake. Answering otherwise would clear the only copy on a guess.
            let held: TrainingSession?
            do {
                held = try await session(id)
            } catch {
                throw failure
            }
            switch held {
            case .completed?:
                return
            case nil:
                // An earlier delivery may have taken this same path and landed without its
                // answer; the session is gone either way, so the workout is found by its start.
                if case .completed = try await nearby(workout) { return }
                try await logWhole(workout, period: period, effort: effort)
            case .active?, .planned?, .skipped?, .unrecognised?:
                throw failure
            }
        }
    }

    private func logWhole(
        _ workout: ActiveWorkout, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws {
        _ = try await sessions.logCompleted(
            sets: workout.sets.map(\.command),
            period: period,
            effort: effort,
            notes: workout.notes,
            programId: nil)
    }

    // MARK: - Withdrawing

    /// Removes the session a workout the athlete is throwing away was holding. Best-effort: a
    /// session left active is visible in the Train tab and can be discarded from there.
    ///
    /// **A session the server has completed is never deleted.** The athlete reaches this from a
    /// finish that looked refused, which a lost response produces just as readily as a genuine
    /// failure, so "discard" can mean "throw away the workout I just saved".
    func withdraw(_ workout: ActiveWorkout) async {
        switch workout.destination {
        case .afterTheFact:
            return
        case .liveSession(let id):
            if case .completed? = try? await sessions.session(id: id) { return }
            try? await sessions.delete(id: id)
        case .deferredStart:
            // Only a session this workout opened. A plan still waiting is left planned: the athlete
            // threw away a workout, not the plan for it.
            guard case .active(let id)? = try? await copy(of: workout) else { return }
            try? await sessions.delete(id: id)
        }
    }

    // MARK: - What the server holds

    /// The plan first, where there is one, because a plan the device started is found by id; then
    /// the sessions around the start, for everything else.
    private func copy(of workout: ActiveWorkout) async throws -> ServerCopy {
        if let plan = workout.plannedSessionId,
           let copy = ServerCopy.ofPlan(try await session(plan), of: workout) {
            return copy
        }
        return try await nearby(workout)
    }

    /// The session, or `nil` when the server says there is none.
    private func session(_ id: UUID) async throws -> TrainingSession? {
        do {
            return try await sessions.session(id: id)
        } catch APIError.notFound {
            return nil
        }
    }

    private func nearby(_ workout: ActiveWorkout) async throws -> ServerCopy {
        let found = try await sessions.sessions(
            from: workout.startedAt.addingTimeInterval(-Self.searchMargin),
            to: workout.startedAt.addingTimeInterval(Self.searchMargin))
        return ServerCopy.among(found, of: workout)
    }

    /// The active session a start answered with. A 2xx carrying anything else would have the
    /// device push sets at a session that refuses them, so it is a refusal here instead.
    private static func activeId(_ session: TrainingSession) throws -> UUID {
        guard case .active(let active) = session else {
            throw APIError.refused(detail: "That session did not become an active one.")
        }
        return active.envelope.trainingSessionId
    }
}

private extension APIError {
    /// Whether a start was refused because the plan is no longer there to start: a 409 (it was
    /// started, skipped or completed elsewhere) or a 404 (it was removed).
    var losesThePlan: Bool {
        switch self {
        case .refused, .notFound: true
        case .unauthenticated, .forbidden, .invalid, .upstream, .server, .http, .transport, .decoding: false
        }
    }
}
