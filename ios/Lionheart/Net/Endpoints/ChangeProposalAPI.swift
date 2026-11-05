import Foundation

/// The athlete's answers to what Tempo proposed in the thread (`docs/api-surface.md` → Proposed
/// changes).
///
/// Both answer with the proposal settled. A confirmation the service refused is still a 200 — the
/// card says why nothing was written — while a card already answered elsewhere is a 409, stated and
/// then read again.
struct ChangeProposalAPI: Sendable {
    let client: APIClient

    private static let root = "/api/chat/proposals"

    func confirm(_ changeProposalId: UUID) async throws -> ChangeProposal {
        try await client.post("\(Self.root)/\(changeProposalId.uuidString)/confirm")
    }

    func decline(_ changeProposalId: UUID) async throws -> ChangeProposal {
        try await client.post("\(Self.root)/\(changeProposalId.uuidString)/decline")
    }
}
