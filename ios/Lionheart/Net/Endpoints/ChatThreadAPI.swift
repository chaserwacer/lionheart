import Foundation

/// The continuing thread's routes (`docs/api-surface.md` → Chat thread and memory).
///
/// Sending is one blocking round trip for the whole exchange, as in a standalone conversation, and a
/// failed exchange keeps nothing — the athlete's words included — so the store holds them until the
/// server has answered. The answer is the whole session the exchange went into, which is how the
/// screen learns that a message opened a new one.
struct ChatThreadAPI: Sendable {
    let client: APIClient

    private static let root = "/api/chat/thread"

    /// The newest sessions, remembered so the thread reads back with no signal.
    func latest(remembering key: ReadKey) async throws -> Remembered<ChatThread> {
        try await client.get(Self.root, remembering: key)
    }

    /// The sessions that opened before `instant`: the next stretch back.
    func sessions(openedBefore instant: Date) async throws -> ChatThread {
        try await client.get(Self.root, query: [URLQueryItem(name: "before", value: Self.wire(instant))])
    }

    func send(_ text: String) async throws -> ThreadSession {
        try await client.post(
            "\(Self.root)/messages", body: SendThreadMessageBody(content: text), timeout: HTTP.modelPatience)
    }

    /// An instant as the server binds it: ISO-8601 in UTC, with its fraction.
    private static func wire(_ instant: Date) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.string(from: instant)
    }
}

private struct SendThreadMessageBody: Encodable, Sendable {
    let content: String
}
