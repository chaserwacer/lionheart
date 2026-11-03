import Foundation

@testable import Lionheart

/// A server that accepts everything, for suites whose claims are not about sync.
///
/// Shared by the Live Activity and rest-alarm suites, which both need a store that can reach the
/// end of its completing and discarding paths without the server being the subject. The suite in
/// `ActiveWorkoutStoreTests` keeps its own, richer recorder, because there the server *is* the
/// subject.
struct AnAgreeableServer: WorkoutSessionWriting {
    func session(id: UUID) async throws -> TrainingSession { Self.active(id) }

    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession {
        Self.active(id)
    }

    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession { Self.active(id) }

    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession {
        Self.active(id)
    }

    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String,
        programId: UUID?
    ) async throws -> TrainingSession {
        Self.active(AWorkout.session)
    }

    func delete(id: UUID) async throws {}

    private static func active(_ id: UUID) -> TrainingSession {
        .active(TrainingSession.Active(
            envelope: TrainingSession.Envelope(
                trainingSessionId: id,
                notes: "",
                provenance: .user,
                trainingProgramId: nil,
                createdAt: nil,
                date: CalendarDay(year: 2026, month: 11, day: 2)),
            startedAt: AWorkout.start,
            sets: []))
    }
}
