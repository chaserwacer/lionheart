import Foundation

/// The Apple Health routes: offering a workout for import, and reading an imported run back.
///
/// An offer is idempotent on the workout's Health UUID, so the sync can resend whatever it is
/// unsure of. A run reads back whole, route and all, and is remembered for reading with no signal.
struct HealthAPI: Sendable {
    let client: APIClient

    func offer(_ workout: HealthWorkoutImport) async throws -> HealthImportOutcome {
        try await client.post("/api/health/workouts", body: workout)
    }

    func run(_ trainingSessionId: UUID, remembering key: ReadKey) async throws -> Remembered<RecordedRun> {
        try await client.get(
            "/api/training/sessions/\(trainingSessionId.uuidString)/recording",
            as: RecordedRun.self,
            remembering: key)
    }
}
