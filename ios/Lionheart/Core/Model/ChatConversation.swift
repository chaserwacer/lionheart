import Foundation

/// Who said a thing in a conversation.
///
/// The server's `LHChatMessageRole`, integer-valued like every other enum on this wire. It has to
/// be received rather than inferred: the stored message types are distinct server-side, and the
/// chronological projection is a merge of two tables, so the moment one turn does not arrive in a
/// user/model pair every later turn would be attributed to the wrong speaker.
enum ChatRole: Equatable, Sendable {
    case athlete
    case model
    /// The result of a tool the model called. Stored, but not a turn in the conversation the
    /// athlete is reading.
    case tool
    case system
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .athlete
        case 1: self = .model
        case 2: self = .tool
        case 3: self = .system
        default: self = .unrecognised(wire ?? -1)
        }
    }

    /// Whether this role's messages belong in the transcript the athlete scrolls.
    ///
    /// Total, so a role added later has to decide rather than defaulting into the conversation —
    /// a tool result rendered as the model's answer is the failure this guards.
    var isShownInTranscript: Bool {
        switch self {
        case .athlete, .model: true
        case .tool, .system, .unrecognised: false
        }
    }
}

/// One turn in a conversation.
struct ChatMessage: Equatable, Sendable, Identifiable {
    let chatMessageId: UUID
    let role: ChatRole
    let text: String
    let createdAt: Date?

    var id: UUID { chatMessageId }
}

extension ChatMessage: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let chatMessageId = container.lenient(UUID.self, "chatMessageItemID") else {
            // A turn with no id has no stable identity, so the transcript would animate as though
            // the row had been replaced on every refresh.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A chat message without an id cannot be placed in a transcript."))
        }

        self.init(
            chatMessageId: chatMessageId,
            role: ChatRole(wire: container.lenient(Int.self, "role")),
            text: container.lenient("content", or: ""),
            createdAt: container.instant("creationTime"))
    }
}

/// A conversation with the model: its name, when it was last added to, and its turns.
///
/// `messages` is empty on the list route — which sends `null` for it — and populated on the single
/// read. That is the server's shape rather than a thing worth two types: the list is for choosing a
/// conversation, and choosing one is what loads its turns.
struct ChatConversation: Equatable, Sendable, Identifiable {
    let chatConversationId: UUID
    let name: String
    let createdAt: Date?
    let lastUpdate: Date?
    let messages: [ChatMessage]

    var id: UUID { chatConversationId }

    /// The turns the athlete reads, in the order they happened.
    ///
    /// The server orders by creation time already; sorting again costs nothing and means a screen
    /// never depends on that promise holding for a projection that merges two tables.
    var transcript: [ChatMessage] {
        messages
            .filter(\.role.isShownInTranscript)
            .sorted { ($0.createdAt ?? .distantPast) < ($1.createdAt ?? .distantPast) }
    }

    /// What to show as the title of a conversation the athlete never named.
    var title: String { name.nonEmpty ?? "Untitled conversation" }
}

extension ChatConversation: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let chatConversationId = container.lenient(UUID.self, "chatConversationID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A conversation without an id cannot be opened or continued."))
        }

        self.init(
            chatConversationId: chatConversationId,
            name: container.lenient("name", or: ""),
            createdAt: container.instant("createdAt"),
            lastUpdate: container.instant("lastUpdate"),
            messages: container.lenientList(ChatMessage.self, "messages"))
    }
}

/// What the chat model is given about the athlete: the two standing tiers a conversation opens
/// with, and the briefing his next message will carry.
///
/// Each tier carries its own as-of instant, because that is what makes an answer checkable. The
/// briefing (`today`) has none: the server reads it fresh for every message, and it states its own
/// time in its first line.
struct AthleteContextCard: Equatable, Sendable {
    let stableProfile: String
    let stableProfileAsOf: Date?
    let recentState: String
    let recentStateAsOf: Date?
    let today: String

    var isEmpty: Bool { stableProfile.nonEmpty == nil && recentState.nonEmpty == nil && today.nonEmpty == nil }
}

extension AthleteContextCard: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            stableProfile: container.lenient("stableProfileText", or: ""),
            stableProfileAsOf: container.instant("stableProfileAsOf"),
            recentState: container.lenient("recentStateText", or: ""),
            recentStateAsOf: container.instant("recentStateAsOf"),
            today: container.lenient("todayText", or: ""))
    }
}
