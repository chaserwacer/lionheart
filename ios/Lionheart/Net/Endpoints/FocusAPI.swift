import Foundation

/// The focus routes (`docs/api-surface.md` → Focus).
///
/// Pinning and revising carry the same terms; unpinning is a transition dated by the device,
/// because "the day I stopped" is the athlete's calendar day and the server has no zone to read
/// one in. A revision or an unpin on a pin already unpinned answers 409, which arrives as
/// `APIError.refused` and is stated, never retried.
struct FocusAPI: Sendable {
    let client: APIClient

    private static let root = "/api/training/focus"

    /// Every pin, pinned and unpinned, remembered so the list reads with no signal.
    func pins(remembering key: ReadKey) async throws -> Remembered<[FocusPin]> {
        try await client.get(Self.root, as: LenientList<FocusPin>.self, remembering: key)
            .map(\.elements)
    }

    func pin(_ movementDataId: UUID, _ draft: FocusDraft) async throws -> FocusPin {
        try await client.post(Self.root, body: PinFocusRequest(movementDataId: movementDataId, draft))
    }

    func revise(_ focusPinId: UUID, to draft: FocusDraft) async throws -> FocusPin {
        try await client.patch("\(Self.root)/\(focusPinId.uuidString)", body: FocusTermsRequest(draft))
    }

    func unpin(_ focusPinId: UUID, on day: CalendarDay) async throws -> FocusPin {
        try await client.post(
            "\(Self.root)/\(focusPinId.uuidString)/unpin", body: UnpinFocusRequest(on: day.wire))
    }

    func delete(_ focusPinId: UUID) async throws {
        try await client.delete("\(Self.root)/\(focusPinId.uuidString)")
    }
}

/// `POST /api/training/focus`.
private struct PinFocusRequest: Encodable, Sendable {
    let movementDataId: UUID
    let starts: String
    let ends: String
    let sessionsPerWeek: Int
    let target: LiftTargetRequest?

    init(movementDataId: UUID, _ draft: FocusDraft) {
        let terms = FocusTermsRequest(draft)
        self.movementDataId = movementDataId
        starts = terms.starts
        ends = terms.ends
        sessionsPerWeek = terms.sessionsPerWeek
        target = terms.target
    }
}

/// `PATCH /api/training/focus/{id}`: the terms, replaced whole.
private struct FocusTermsRequest: Encodable, Sendable {
    let starts: String
    let ends: String
    let sessionsPerWeek: Int
    /// Absent for no target — the request's flat spelling of the server's two cases.
    let target: LiftTargetRequest?

    init(_ draft: FocusDraft) {
        starts = draft.horizon.starts.wire
        ends = draft.horizon.ends.wire
        sessionsPerWeek = draft.sessionsPerWeek
        target = switch draft.target {
        case .lift(let weight, let unit, let reps): LiftTargetRequest(weight: weight, unit: unit.wire, reps: reps)
        // A target this build cannot read was never set from it, so a revision sends none rather
        // than inventing one.
        case .none, .unrecognised: nil
        }
    }
}

private struct LiftTargetRequest: Encodable, Sendable {
    let weight: Double
    let unit: Int
    let reps: Int
}

private struct UnpinFocusRequest: Encodable, Sendable {
    let on: String
}
