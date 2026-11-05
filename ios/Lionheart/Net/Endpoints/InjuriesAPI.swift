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

    /// Every injury on record, active or not, remembered under `key`. There is no server-side
    /// filter for active ones, so the caller narrows.
    func injuries(remembering key: ReadKey) async throws -> Remembered<[Injury]> {
        try await client.get(
            "/api/injury/get-user-injuries", as: LenientList<Injury>.self, remembering: key
        ).map(\.elements)
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

    /// Starts tracking a new injury. The server opens it as active, with no events.
    func create(_ draft: InjuryDraft) async throws -> Injury {
        try await client.post("/api/injury/create", body: CreateInjuryRequest(draft))
    }

    /// Logs a treatment or a check-in against an injury. The route answers with the injury and all
    /// its events, and stamps the event with the server's own clock — there is no way to backdate
    /// one.
    func addEvent(_ draft: InjuryEventDraft, to injuryId: UUID) async throws -> Injury {
        try await client.post("/api/injury/add-event", body: CreateInjuryEventRequest(draft, injuryId: injuryId))
    }

    func delete(id: UUID) async throws {
        try await client.delete("/api/injury/delete/\(id.uuidString)")
    }

    /// Removes one treatment or check-in. The injury itself stays.
    func deleteEvent(id: UUID) async throws {
        try await client.delete("/api/injury/event/\(id.uuidString)")
    }
}

/// `POST /api/injury/create`. The day travels as midnight with no offset, because the server reads
/// it as a `DateTime` and keeps only its date — an offset could move it to the day either side.
private struct CreateInjuryRequest: Encodable, Sendable {
    let name: String
    let injuryDate: String
    let notes: String
    let families: [String]

    init(_ draft: InjuryDraft) {
        name = draft.name
        injuryDate = "\(draft.startedOn.wire)T00:00:00"
        notes = draft.notes
        families = draft.families.map(\.wire)
    }
}

/// `POST /api/injury/add-event`. `injuryType` is the server's enum as an integer; no training
/// session or movements are attached from here.
private struct CreateInjuryEventRequest: Encodable, Sendable {
    let injuryID: UUID
    let trainingSessionID: UUID?
    let notes: String
    let painLevel: Int
    let injuryType: Int
    let movementIDs: [UUID]

    init(_ draft: InjuryEventDraft, injuryId: UUID) {
        injuryID = injuryId
        trainingSessionID = nil
        notes = draft.notes
        painLevel = draft.painLevel
        injuryType = draft.kind.wire
        movementIDs = []
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
