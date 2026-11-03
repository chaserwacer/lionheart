import Foundation

/// The chat routes, read off `Endpoints/ChatEndpoints/` — they predate the remodel and are not in
/// `docs/api-surface.md`, which is why the paths carry verbs.
///
/// Two behaviours of `POST /message/process` shape the whole Chat screen and are worth knowing
/// before changing it:
///
/// - **It is one blocking round trip for the whole exchange.** The model answers over the athlete's
///   own data through a tool loop, so a reply takes tens of seconds against a local model. There is
///   no streaming route and no progress channel; the UI's only honest option is to say it is
///   working and keep the athlete's question on screen.
/// - **A failed exchange does not store the question.** The server adds the athlete's message to
///   the change tracker and saves only once the model has answered, so a refusal loses it. The
///   store therefore keeps the typed words and puts them back in the composer rather than trusting
///   the server to hold them.
struct ChatAPI: Sendable {
    let client: APIClient

    /// Every conversation, newest activity first. Their `messages` arrive null on this route; a
    /// conversation's turns come from `conversation(id:)`.
    func conversations() async throws -> [ChatConversation] {
        try await client.get("/api/chat/conversations", as: LenientList<ChatConversation>.self).elements
    }

    func conversation(id: UUID) async throws -> ChatConversation {
        try await client.get("/api/chat/conversation/\(id.uuidString)")
    }

    /// The conversation last added to. Answers 404 — not 204, whatever the route's attribute says —
    /// when the athlete has never started one, which is an empty state and not a failure.
    func mostRecentConversation() async throws -> ChatConversation {
        try await client.get("/api/chat/conversation/most-recent")
    }

    func createConversation(named name: String) async throws -> ChatConversation {
        try await client.post("/api/chat/conversation/create", body: ConversationNameBody(name: name))
    }

    /// Sends one turn and waits for the whole exchange. The reply is the model's final message.
    func send(_ text: String, to conversationId: UUID) async throws -> ChatMessage {
        try await client.post(
            "/api/chat/message/process",
            body: SendMessageBody(chatConversationID: conversationId, content: text))
    }

    func deleteConversation(id: UUID) async throws {
        try await client.delete("/api/chat/conversation/\(id.uuidString)")
    }
}

private struct ConversationNameBody: Encodable, Sendable {
    let name: String
}

private struct SendMessageBody: Encodable, Sendable {
    let chatConversationID: UUID
    let content: String
}
