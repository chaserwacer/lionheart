import Foundation

/// A turn in the transcript as the screen draws it, which is not the same list as the server's.
///
/// Two of these are not server records at all. `.pending` is the athlete's question, shown the
/// instant they send it; `.unsent` is that same question after the exchange failed, which is a
/// distinct state because **the server did not keep it** — it adds the message to its change
/// tracker and saves only once the model has answered, so a refused exchange loses the question
/// entirely. Showing it as sent would be a lie the athlete would discover on the next refresh.
enum ChatTurn: Equatable, Sendable, Identifiable {
    case said(ChatMessage)
    /// The athlete's words, in flight. No server id yet, so it carries its own.
    case pending(id: UUID, text: String)
    /// The athlete's words, after the exchange failed and the server kept nothing.
    case unsent(id: UUID, text: String, failure: LoadFailure)

    var id: UUID {
        switch self {
        case .said(let message): message.chatMessageId
        case .pending(let id, _), .unsent(let id, _, _): id
        }
    }

    var text: String {
        switch self {
        case .said(let message): message.text
        case .pending(_, let text), .unsent(_, let text, _): text
        }
    }

    /// Whether the athlete said it. Total, so a turn added later has to decide rather than
    /// defaulting to the model's side of the conversation.
    var isFromAthlete: Bool {
        switch self {
        case .pending, .unsent: true
        case .said(let message):
            switch message.role {
            case .athlete: true
            case .model, .tool, .system, .unrecognised: false
            }
        }
    }
}
