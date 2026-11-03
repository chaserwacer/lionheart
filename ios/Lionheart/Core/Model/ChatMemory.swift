import Foundation

/// What sort of thing the athlete told the chat — the server's `MemoryKind`, integer-valued.
///
/// The kind decides reach: goals, injuries and preferences go into every session, the rest are
/// recalled when a session bears on them.
enum MemoryKind: Equatable, Hashable, Sendable {
    case goal
    case injury
    case preference
    case commitment
    case decision
    case fact
    case unrecognised(Int)

    /// The kinds the athlete can choose when correcting a memory, in the order a list shows them.
    static let known: [MemoryKind] = [.goal, .injury, .preference, .commitment, .decision, .fact]

    init(wire: Int?) {
        switch wire {
        case 0: self = .goal
        case 1: self = .injury
        case 2: self = .preference
        case 3: self = .commitment
        case 4: self = .decision
        case 5: self = .fact
        default: self = .unrecognised(wire ?? -1)
        }
    }

    var wire: Int {
        switch self {
        case .goal: 0
        case .injury: 1
        case .preference: 2
        case .commitment: 3
        case .decision: 4
        case .fact: 5
        case .unrecognised(let value): value
        }
    }

    /// Whether every session starts with memories of this kind.
    var isStanding: Bool {
        switch self {
        case .goal, .injury, .preference: true
        case .commitment, .decision, .fact, .unrecognised: false
        }
    }
}

/// Who wrote a memory's current version.
enum MemorySource: Equatable, Sendable {
    /// Distilled from what he said in the thread.
    case chat
    /// Written by the athlete, correcting what the chat kept.
    case athlete
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .chat
        case 1: self = .athlete
        default: self = .unrecognised(wire ?? -1)
        }
    }
}

/// A message a memory was drawn from, clipped to a line or two.
struct MemoryQuote: Equatable, Sendable, Identifiable {
    let messageId: UUID
    let byAthlete: Bool
    let said: String
    let at: Date?

    var id: UUID { messageId }
}

extension MemoryQuote: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let messageId = container.lenient(UUID.self, "messageId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A quote names the message it came from."))
        }
        self.init(
            messageId: messageId,
            byAthlete: container.lenient("byAthlete", or: true),
            said: container.lenient("said", or: ""),
            at: container.instant("at"))
    }
}

/// A version a memory used to read, and when it stopped holding.
struct MemoryVersion: Equatable, Sendable {
    let kind: MemoryKind
    let text: String
    let recordedAt: Date?
    let replacedAt: Date?
}

extension MemoryVersion: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            kind: MemoryKind(wire: container.lenient(Int.self, "kind")),
            text: container.lenient("text", or: ""),
            recordedAt: container.instant("recordedAt"),
            replacedAt: container.instant("replacedAt"))
    }
}

/// Something the athlete told the chat that Lionheart keeps, with where it came from and what it
/// said before.
struct ChatMemory: Equatable, Sendable, Identifiable {
    let memoryId: UUID
    let kind: MemoryKind
    let text: String
    let recordedAt: Date?
    let source: MemorySource
    let quotes: [MemoryQuote]
    /// The versions this one replaced, newest first.
    let earlier: [MemoryVersion]

    var id: UUID { memoryId }
}

extension ChatMemory: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let memoryId = container.lenient(UUID.self, "memoryId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A memory without an id cannot be corrected or deleted."))
        }
        self.init(
            memoryId: memoryId,
            kind: MemoryKind(wire: container.lenient(Int.self, "kind")),
            text: container.lenient("text", or: ""),
            recordedAt: container.instant("recordedAt"),
            source: MemorySource(wire: container.lenient(Int.self, "source")),
            quotes: container.lenientList(MemoryQuote.self, "quotes"),
            earlier: container.lenientList(MemoryVersion.self, "earlier"))
    }
}
