import Foundation

/// The continuing thread's routes (`docs/api-surface.md` → Chat thread and memory).
///
/// A message is answered as it is written, over server-sent events. The exchange is the server's
/// to finish once it has accepted it: it is stored even when the connection drops partway, and a
/// failed one keeps nothing — the athlete's words included — so the store holds them until the
/// server has said which.
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

    /// Sends `text` and returns the answer's events as they arrive.
    ///
    /// Throws only when the server took nothing on — it refused the message, or could not be
    /// reached. Once this returns, the exchange is the server's: a failure while reading the events
    /// is a lost connection, not a lost answer.
    func ask(_ text: String) async throws -> ThreadReplies {
        ThreadReplies(events: try await client.postReceivingEvents(
            "\(Self.root)/messages/stream", body: SendThreadMessageBody(content: text)))
    }

    /// One event as this app reads it. Total: an event that will not decode is passed over rather
    /// than ending the answer, since the one that ends it is still to come.
    static func reply(_ event: ServerSentEvent) -> ThreadReplyEvent {
        let data = Data(event.data.utf8)
        switch event.name {
        case "delta":
            guard let delta = try? Wire.decoder().decode(Delta.self, from: data) else { break }
            return .wrote(delta.text)
        case "tools":
            guard let tools = try? Wire.decoder().decode(Tools.self, from: data) else { break }
            return .consulting(tools: tools.tools)
        case "done":
            guard let session = try? Wire.decoder().decode(ThreadSession.self, from: data) else {
                // Stored, but unreadable here: reading the thread back is how the screen recovers,
                // exactly as from a dropped connection.
                break
            }
            return .stored(session)
        case "failed":
            let status = (try? Wire.decoder().decode(Status.self, from: data))?.status ?? 500
            return .refused(HTTP.failure(status: status, body: data) ?? .http(status: status, detail: nil))
        default:
            break
        }
        return .unrecognised(name: event.name)
    }

    /// An instant as the server binds it: ISO-8601 in UTC, with its fraction.
    private static func wire(_ instant: Date) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.string(from: instant)
    }

    private struct Delta: Decodable { let text: String }
    private struct Tools: Decodable { let tools: [String] }
    private struct Status: Decodable { let status: Int }
}

/// The answer to one message, event by event, as the server sends it.
struct ThreadReplies: AsyncSequence {
    typealias Element = ThreadReplyEvent

    let events: ServerSentEvents<AsyncLineSequence<URLSession.AsyncBytes>>

    struct AsyncIterator: AsyncIteratorProtocol {
        var events: ServerSentEvents<AsyncLineSequence<URLSession.AsyncBytes>>.AsyncIterator

        mutating func next() async throws -> ThreadReplyEvent? {
            try await events.next().map(ChatThreadAPI.reply)
        }
    }

    func makeAsyncIterator() -> AsyncIterator {
        AsyncIterator(events: events.makeAsyncIterator())
    }
}

private struct SendThreadMessageBody: Encodable, Sendable {
    let content: String
}
