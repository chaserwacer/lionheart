import Foundation

/// What the active workout needs from the server, and nothing else.
///
/// A seam at the **network** boundary, which is the one place in this app a stand-in earns its
/// keep: the thing being stood in for is the server, and a unit test cannot run one. Everything
/// else the store depends on — the document, the clock — is a value or a file and runs as itself.
///
/// It is exactly as wide as the workout's own writes. Reading sessions, planning them and skipping
/// them are the Train tab's business and are deliberately absent, so that nothing about this
/// protocol suggests the store may reach for them.
protocol WorkoutSessionWriting: Sendable {
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
