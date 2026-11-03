import Foundation

/// The training-session routes, one method per route in `docs/api-surface.md`.
///
/// The server exposes **transitions, not shapes**. There is no `PUT /sessions/{id}`; a session
/// moves state through its own route, and asking for a transition from the wrong state answers
/// 409, which arrives here as `APIError.refused` and is a refusal the UI states rather than an
/// error it swallows. Every one of these answers with the session in its new state, so a caller
/// never has to re-read to find out what happened.
///
/// `PUT /prescriptions` is not here. Nothing in the app plans a session yet — the Train tab starts
/// and logs them — and a route with no caller is surface to keep correct for nothing.
struct TrainingSessionsAPI: Sendable {
    let client: APIClient

    private static let root = "/api/training/sessions"

    /// Every session whose own time-truth falls inside the window.
    ///
    /// Each lifecycle state is matched on a different column server-side — a completed session on
    /// its stamped start instant, a planned or skipped one on the day it was planned for — so a
    /// window reaching into the future does return planned sessions, which is what the Today
    /// screen's "next session" depends on.
    func sessions(from: Date, to: Date) async throws -> [TrainingSession] {
        try await client.get(
            Self.root, query: Self.window(from, to), as: LenientList<TrainingSession>.self).elements
    }

    /// The same window as a screen reads it: the answer is kept under `key`, and given back with
    /// its time when the server cannot be reached.
    func sessions(
        from: Date, to: Date, remembering key: ReadKey
    ) async throws -> Remembered<[TrainingSession]> {
        try await client.get(
            Self.root, query: Self.window(from, to), as: LenientList<TrainingSession>.self,
            remembering: key
        ).map(\.elements)
    }

    private static func window(_ from: Date, _ to: Date) -> [URLQueryItem] {
        [
            URLQueryItem(name: "from", value: WireDate.wire(from: from)),
            URLQueryItem(name: "to", value: WireDate.wire(from: to)),
        ]
    }

    func session(id: UUID) async throws -> TrainingSession {
        try await client.get("\(Self.root)/\(id.uuidString)")
    }

    // MARK: - Starting

    /// Planned → Active.
    func start(id: UUID, at startedAt: Date) async throws -> TrainingSession {
        try await client.post(
            "\(Self.root)/\(id.uuidString)/start",
            body: StartSessionRequest(startedAt: startedAt))
    }

    /// An unplanned start: nothing was planned and the athlete began training — from a routine,
    /// where `routineId` names one.
    func startUnplanned(
        at startedAt: Date, notes: String = "", programId: UUID? = nil, routineId: UUID? = nil
    ) async throws -> TrainingSession {
        try await client.post(
            "\(Self.root)/active",
            body: StartUnplannedSessionRequest(
                startedAt: startedAt, notes: notes, trainingProgramId: programId, routineId: routineId))
    }

    /// A session that already happened, in one request.
    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String = "",
        programId: UUID? = nil,
        routineId: UUID? = nil
    ) async throws -> TrainingSession {
        try await client.post(
            "\(Self.root)/completed",
            body: LogCompletedSessionRequest(
                sets: sets, period: period, effort: effort,
                notes: notes, trainingProgramId: programId, routineId: routineId))
    }

    /// Last time on every movement the athlete has trained: each one's sets from the most recent
    /// completed session holding it. Remembered, because the previous column is wanted most in a
    /// basement.
    func previousPerformances(remembering key: ReadKey) async throws -> Remembered<[PreviousPerformance]> {
        try await client.get(
            "/api/training/previous-performances", as: LenientList<PreviousPerformance>.self,
            remembering: key
        ).map(\.elements)
    }

    // MARK: - While it runs

    /// Replaces the session's whole ordered set list.
    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession {
        try await client.put(
            "\(Self.root)/\(id.uuidString)/sets",
            body: ReplaceSetsRequest(sets: sets))
    }

    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession {
        try await client.patch(
            "\(Self.root)/\(id.uuidString)/notes",
            body: SessionNotesRequest(notes: notes))
    }

    // MARK: - Ending

    /// Active → Completed.
    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession {
        try await client.post(
            "\(Self.root)/\(id.uuidString)/complete",
            body: CompleteSessionRequest(period: period, effort: effort))
    }

    /// Corrects a completed session: its sets, its period and its notes, restated whole. A change
    /// to the sets or the start has the server re-detect every record over the whole history, and
    /// is refused (409) while any record is withdrawn. Restating the same correction twice leaves
    /// the same session, so a save whose answer was lost is safe to send again.
    func revise(id: UUID, sets: [SetCommand], period: PeriodRequest, notes: String) async throws -> TrainingSession {
        try await client.post(
            "\(Self.root)/\(id.uuidString)/revise",
            body: ReviseSessionRequest(sets: sets, period: period, notes: notes))
    }

    /// Planned → Skipped. Terminal, and kept: the skip is data.
    func skip(id: UUID) async throws -> TrainingSession {
        try await client.post("\(Self.root)/\(id.uuidString)/skip")
    }

    /// Removes the session entirely — what discarding a workout the athlete never meant to start
    /// does, so an accidental tap does not leave an empty session in their history for ever.
    func delete(id: UUID) async throws {
        try await client.delete("\(Self.root)/\(id.uuidString)")
    }
}

/// Notes are editable in every lifecycle state — they are commentary, not state.
struct SessionNotesRequest: Encodable, Sendable {
    let notes: String
}
