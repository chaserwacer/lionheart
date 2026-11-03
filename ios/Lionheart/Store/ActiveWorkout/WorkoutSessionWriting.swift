import Foundation

/// What the active workout needs from the server, and nothing else.
///
/// A seam at the **network** boundary, which is the one place in this app a stand-in earns its
/// keep: the thing being stood in for is the server, and a unit test cannot run one. Everything
/// else the store depends on — the document, the clock — is a value or a file and runs as itself.
///
/// It is exactly as wide as the workout's own writes, plus the one read that makes the destructive
/// paths safe. Planning sessions and skipping them are the Train tab's business and are
/// deliberately absent, so that nothing about this protocol suggests the store may reach for them.
protocol WorkoutSessionWriting: Sendable {
    /// What the server currently believes about a session.
    ///
    /// Reading earns its place here because the store has two paths that destroy work — clearing
    /// the local document and deleting the session — and neither can be taken safely on the
    /// device's belief alone. If a completing call commits on the server and its *response* is
    /// lost, the device still thinks the workout is unfinished; the athlete is then offered
    /// "discard", and deleting would throw away a workout the server had saved. Asking is the only
    /// way to tell that apart from a workout that genuinely never landed.
    func session(id: UUID) async throws -> TrainingSession

    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession
    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession
    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession
    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String,
        programId: UUID?
    ) async throws -> TrainingSession
    func delete(id: UUID) async throws
}

extension TrainingSessionsAPI: WorkoutSessionWriting {}
