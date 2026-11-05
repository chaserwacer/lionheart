import Foundation

/// One session of the continuing chat thread: when it opened, its turns, and the changes Tempo
/// proposed in it.
///
/// The server cuts the thread into sessions — a new one after three hours idle, or once a session
/// grows full — and the model only ever reads the one a message lands in. The screen draws them as
/// one transcript with each opening as a seam, so the cut is visible without being in the way.
struct ThreadSession: Equatable, Sendable, Identifiable {
    let sessionId: UUID
    let openedAt: Date
    let messages: [ChatMessage]
    let proposals: [ChangeProposal]

    var id: UUID { sessionId }

    /// The turns the athlete reads, in the order they happened.
    var transcript: [ChatMessage] {
        messages
            .filter(\.role.isShownInTranscript)
            .sorted { ($0.createdAt ?? .distantPast) < ($1.createdAt ?? .distantPast) }
    }
}

extension ThreadSession: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        // A session with no id cannot be matched against the one an exchange answers with, and one
        // with no opening has no place in the thread.
        guard let sessionId = container.lenient(UUID.self, "sessionId"),
              let openedAt = container.instant("openedAt")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A thread session needs an id and an opening instant."))
        }

        self.init(
            sessionId: sessionId,
            openedAt: openedAt,
            messages: container.lenientList(ChatMessage.self, "messages"),
            proposals: container.lenientList(ChangeProposal.self, "proposals"))
    }
}

/// As much of the thread as the screen has read: a run of sessions, oldest first, and whether
/// there are older ones still on the server.
struct ChatThread: Equatable, Sendable {
    let sessions: [ThreadSession]
    let hasEarlier: Bool

    static let empty = ChatThread(sessions: [], hasEarlier: false)

    /// Whether the athlete has never said anything to the thread.
    var isEmpty: Bool { sessions.isEmpty }

    /// The cursor for the next stretch back: the opening of the oldest session read.
    var oldestOpening: Date? { sessions.first?.openedAt }

    /// Every turn held, by id: what the thread already said before a question was sent.
    var messageIds: Set<UUID> { Set(sessions.flatMap { $0.messages.map(\.chatMessageId) }) }

    /// An older stretch put in front of this one. Its own `hasEarlier` is the thread's now, and a
    /// session both stretches hold — the server's page edges are instants — is kept once.
    func prepending(_ earlier: ChatThread) -> ChatThread {
        let known = Set(sessions.map(\.sessionId))
        return ChatThread(
            sessions: earlier.sessions.filter { !known.contains($0.sessionId) } + sessions,
            hasEarlier: earlier.hasEarlier)
    }

    /// The thread with a proposal as its answer left it, in whichever session holds it.
    func with(_ proposal: ChangeProposal) -> ChatThread {
        ChatThread(
            sessions: sessions.map { session in
                ThreadSession(
                    sessionId: session.sessionId,
                    openedAt: session.openedAt,
                    messages: session.messages,
                    proposals: session.proposals.map { $0.changeProposalId == proposal.changeProposalId ? proposal : $0 })
            },
            hasEarlier: hasEarlier)
    }

    /// The thread with a session as an exchange left it: replaced where it is already held, added
    /// at the end when the exchange opened it.
    func with(_ session: ThreadSession) -> ChatThread {
        guard sessions.contains(where: { $0.sessionId == session.sessionId }) else {
            return ChatThread(sessions: sessions + [session], hasEarlier: hasEarlier)
        }
        return ChatThread(
            sessions: sessions.map { $0.sessionId == session.sessionId ? session : $0 },
            hasEarlier: hasEarlier)
    }
}

extension ChatThread: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            sessions: container.lenientList(ThreadSession.self, "sessions")
                .sorted { $0.openedAt < $1.openedAt },
            hasEarlier: container.lenient("hasEarlier", or: false))
    }
}
