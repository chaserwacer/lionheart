import Foundation

/// The injury routes, read off `Endpoints/InjuryEndpoints/` — also pre-remodel, also not in
/// `docs/api-surface.md`.
///
/// The update route is a **whole-record replace**, not a patch: `UpdateInjuryRequest` carries the
/// name, the notes and `isActive` together and writes all three. So the one member the Body tab
/// changes travels with the two it does not, which is why `setTracking` takes the injury rather
/// than its id — the record it is holding *is* the rest of the request.
struct InjuriesAPI: Sendable {
    let client: APIClient

    /// Every injury on record, active or not. There is no server-side filter for active ones, so
    /// the caller narrows.
    func injuries() async throws -> [Injury] {
        try await client.get("/api/injury/get-user-injuries", as: LenientList<Injury>.self).elements
    }

    /// Writes whether the injury is still being tracked, carrying its name and notes through
    /// unchanged.
    ///
    /// The route answers with the injury in its new state, so no caller has to re-read to find out
    /// what happened — though the Body tab reloads anyway, since resolving one moves it between two
    /// sections.
    func setTracking(of injury: Injury, to isActive: Bool) async throws -> Injury {
        try await client.put(
            "/api/injury/update", body: UpdateInjuryRequest(injury, isActive: isActive))
    }

    func delete(id: UUID) async throws {
        try await client.delete("/api/injury/delete/\(id.uuidString)")
    }

    /// Removes one treatment or check-in. The injury itself stays.
    func deleteEvent(id: UUID) async throws {
        try await client.delete("/api/injury/event/\(id.uuidString)")
    }
}

/// The whole-record update the route expects. `injuryDate` is deliberately absent: the server does
/// not accept a new one, so when an injury started is not editable from here.
private struct UpdateInjuryRequest: Encodable, Sendable {
    let injuryID: UUID
    let name: String
    let notes: String
    let isActive: Bool

    init(_ injury: Injury, isActive: Bool) {
        injuryID = injury.injuryId
        name = injury.name
        notes = injury.notes
        self.isActive = isActive
    }
}
