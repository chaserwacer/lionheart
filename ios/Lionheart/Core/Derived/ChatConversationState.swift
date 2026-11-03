import Foundation

/// The whole Chat screen: whether a conversation is open, its turns, and whether a reply is out.
///
/// It carries its own not-loaded and unavailable cases instead of sitting inside a `LoadState`,
/// because the two would overlap: `LoadState.loaded(.idle)` and a bare `.idle` are the same screen
/// spelled twice. Six cases, each one a screen the athlete can actually be looking at.
///
/// `.awaitingReply` earns its case rather than being a flag beside the transcript, because the
/// screen genuinely differs while it holds: the composer is disabled, the athlete's question is on
/// screen *unsaved*, and an exchange takes tens of seconds. A Boolean beside a transcript would let
/// "waiting with nothing pending" be written.
enum ChatConversationState: Equatable, Sendable {
    /// The first read is still out.
    case loading
    /// No conversation — the athlete has never asked anything, or just cleared the screen for a
    /// new one. An empty state, never a failure.
    case none
    case reading(ChatConversation)
    /// A question is out. The conversation carries everything already said; the pending turn is
    /// the question itself.
    case awaitingReply(ChatConversation, pending: ChatTurn)
    /// The last exchange failed. The conversation is intact; the question is not in it, because
    /// the server did not keep it.
    case refused(ChatConversation, unsent: ChatTurn)
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
    var turns: [ChatTurn] {
        let said = (conversation?.transcript ?? []).map(ChatTurn.said)
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
    func applying(_ result: Result<ChatConversation, LoadFailure>) -> ChatConversationState {
        switch result {
        case .success(let conversation):
            return .reading(conversation)
        case .failure(let failure):
            return conversation == nil ? .unavailable(failure) : self
        }
    }
}
