import Foundation

/// The training-session routes, one method per route in `docs/api-surface.md`.
///
/// Only the reads Today needs are here. The transition routes (`/start`, `/complete`, `/skip`,
/// `PUT /sets`, `PUT /prescriptions`) and the three creates are deliberately unwritten rather than
/// guessed at — the agent that builds the training screens adds the ones it uses, against the spec.
///
/// Worth knowing before adding one: the server exposes **transitions, not shapes**. There is no
/// `PUT /sessions/{id}`; a session moves state through its own route, and asking for a transition
/// from the wrong state answers 409, which arrives here as `APIError.refused` and is a refusal the
/// UI states rather than an error it swallows.
struct TrainingSessionsAPI: Sendable {
    let client: APIClient

    private static let root = "/api/training/sessions"

    /// Every session whose own time-truth falls inside the window.
    ///
    /// Each lifecycle state is matched on a different column server-side — a completed session on
    /// its stamped start instant, a planned or skipped one on the day it was planned for — so a
    /// window reaching into the future does return planned sessions, which is what the Today
    /// screen's "next session" depends on.
    func sessions(from: Date, to: Date) async throws -> [TrainingSession] {
        try await client.get(
            Self.root,
            query: [
                URLQueryItem(name: "from", value: WireDate.wire(from: from)),
                URLQueryItem(name: "to", value: WireDate.wire(from: to)),
            ],
            as: LenientList<TrainingSession>.self).elements
    }

    func session(id: UUID) async throws -> TrainingSession {
        try await client.get("\(Self.root)/\(id.uuidString)")
    }
}
