import Foundation

/// A turn in the transcript as the screen draws it, which is not the same list as the server's.
///
/// Two of these are not server records at all. `.pending` is the athlete's question, shown the
/// instant they send it; `.unsent` is that same question after the exchange failed, which is a
/// distinct state because **the server did not keep it** — it adds the message to its change
/// tracker and saves only once the model has answered, so a refused exchange loses the question
/// entirely. Showing it as sent would be a lie the athlete would discover on the next refresh.
enum CoachTurn: Equatable, Sendable, Identifiable {
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
    /// defaulting to the coach's side of the conversation.
    var isFromAthlete: Bool {
        switch self {
        case .pending, .unsent: true
        case .said(let message):
            switch message.role {
            case .athlete: true
            case .coach, .tool, .system, .unrecognised: false
            }
        }
    }
}

/// The whole Coach screen: whether a conversation is open, its turns, and whether a reply is out.
///
/// It carries its own not-loaded and unavailable cases instead of sitting inside a `LoadState`,
/// because the two would overlap: `LoadState.loaded(.idle)` and a bare `.idle` are the same screen
/// spelled twice. Six cases, each one a screen the athlete can actually be looking at.
///
/// `.awaitingReply` earns its case rather than being a flag beside the transcript, because the
/// screen genuinely differs while it holds: the composer is disabled, the athlete's question is on
/// screen *unsaved*, and an exchange takes tens of seconds. A Boolean beside a transcript would let
/// "waiting with nothing pending" be written.
enum CoachConversationState: Equatable, Sendable {
    /// The first read is still out.
    case loading
    /// No conversation — the athlete has never asked anything, or just cleared the screen for a
    /// new one. An empty state, never a failure.
    case none
    case reading(ChatConversation)
    /// A question is out. The conversation carries everything already said; the pending turn is
    /// the question itself.
    case awaitingReply(ChatConversation, pending: CoachTurn)
    /// The last exchange failed. The conversation is intact; the question is not in it, because
    /// the server did not keep it.
    case refused(ChatConversation, unsent: CoachTurn)
    /// Nothing could be read, and there is nothing already on screen to keep.
    case unavailable(LoadFailure)

    var conversation: ChatConversation? {
        switch self {
        case .loading, .none, .unavailable: nil
        case .reading(let conversation),
             .awaitingReply(let conversation, _),
             .refused(let conversation, _): conversation
        }
    }

    var isAwaitingReply: Bool {
        switch self {
        case .awaitingReply: true
        case .loading, .none, .reading, .refused, .unavailable: false
        }
    }

    /// Everything to draw, oldest first.
    var turns: [CoachTurn] {
        let said = (conversation?.transcript ?? []).map(CoachTurn.said)
        switch self {
        case .loading, .none, .unavailable, .reading: return said
        case .awaitingReply(_, let pending): return said + [pending]
        case .refused(_, let unsent): return said + [unsent]
        }
    }

    /// The words to put back in the composer, so the athlete does not retype a question the server
    /// threw away.
    var unsentText: String? {
        switch self {
        case .refused(_, let unsent): unsent.text
        case .loading, .none, .reading, .awaitingReply, .unavailable: nil
        }
    }

    /// Whether the athlete may type. The screen is read-only only while an exchange is out —
    /// a failure does not lock the composer, it hands the question back to it.
    var acceptsTyping: Bool {
        switch self {
        case .none, .reading, .refused: true
        case .loading, .awaitingReply, .unavailable: false
        }
    }

    /// Where this state goes when a read finishes.
    ///
    /// The rule that matters is the same one `LoadState.applying` carries: **a failed read never
    /// discards a conversation already on screen.** Losing a transcript because a refresh timed out
    /// would lose the one thing the athlete came back to read.
    func applying(_ result: Result<ChatConversation, LoadFailure>) -> CoachConversationState {
        switch result {
        case .success(let conversation):
            return .reading(conversation)
        case .failure(let failure):
            return conversation == nil ? .unavailable(failure) : self
        }
    }
}
