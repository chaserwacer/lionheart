import Foundation

/// The Apple Health routes: offering a workout for import, and reading an imported run back.
///
/// An offer is idempotent on the workout's Health UUID, so the sync can resend whatever it is
/// unsure of. A run reads back whole, route and all, and is remembered for reading with no signal.
struct HealthAPI: Sendable {
    let client: APIClient

    /// The server's answer to one workout, with its refusal of that one workout restated here, once,
    /// as a reply rather than an error; anything else — the server out of reach — still throws.
    func offer(_ workout: HealthWorkoutImport) async throws -> HealthOfferReply {
        do {
            return .answered(try await client.post("/api/health/workouts", body: workout))
        } catch {
            guard let reason = HealthSyncFailure.refusalOfOneWorkout(error) else { throw error }
            return .refused(reason)
        }
    }

    func run(_ trainingSessionId: UUID, remembering key: ReadKey) async throws -> Remembered<RecordedRun> {
        try await client.get(
            "/api/training/sessions/\(trainingSessionId.uuidString)/recording",
            as: RecordedRun.self,
            remembering: key)
    }
}
