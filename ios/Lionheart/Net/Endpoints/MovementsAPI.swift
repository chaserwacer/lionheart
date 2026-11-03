import Foundation

/// The movement catalogue — what a `movementDataId` on a set actually names.
///
/// A set entry carries only the id, so every screen that lists sets needs this. It is its own
/// endpoint type rather than a method on `TrainingSessionsAPI` because it is reference data with a
/// different lifetime from a session: it changes when the athlete adds a movement, not when they
/// train.
struct MovementsAPI: Sendable {
    let client: APIClient

    func movements() async throws -> [MovementData] {
        try await client.get("/api/movement-datas", as: LenientList<MovementData>.self).elements
    }
}
