import Foundation

/// The injury routes, read off `Endpoints/InjuryEndpoints/` — also pre-remodel, also not in
/// `docs/api-surface.md`.
struct InjuriesAPI: Sendable {
    let client: APIClient

    /// Every injury on record, active or not. There is no server-side filter for active ones, so
    /// the caller narrows.
    func injuries() async throws -> [Injury] {
        try await client.get("/api/injury/get-user-injuries", as: LenientList<Injury>.self).elements
    }
}
