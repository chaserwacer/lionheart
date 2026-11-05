import Foundation

/// Where the thread's one exchange stands. Apart from the transcript because the two vary
/// independently: a thread recalled offline can hold a question that failed to send, and a reply
/// can be awaited over a transcript that is not current.
enum ThreadExchange: Equatable, Sendable {
    case idle
    /// A question is out, on screen but not yet kept by the server, and the answer is arriving.
    case awaitingReply(ChatTurn, ReplyProgress)
    /// The connection went while the answer was arriving. The server finishes and stores it
    /// regardless, so the thread is being read until it shows up.
    case recovering(ChatTurn)
    /// The last question failed and the server kept nothing of it; it goes back to the composer.
    case refused(ChatTurn)

    /// Whether an answer is still to come, which is when nothing else may be asked.
    var isAwaitingReply: Bool {
        switch self {
        case .awaitingReply, .recovering: true
        case .idle, .refused: false
        }
    }

    /// The athlete's own turn still on screen, below everything the server kept.
    var tail: ChatTurn? {
        switch self {
        case .idle: nil
        case .awaitingReply(let turn, _), .recovering(let turn), .refused(let turn): turn
        }
    }

    var unsentText: String? {
        switch self {
        case .refused(let turn): turn.text
        case .idle, .awaitingReply, .recovering: nil
        }
    }
}

/// One row of the thread as it is drawn: a session's opening, a turn, or a change Tempo proposed.
enum ThreadRow: Equatable, Sendable, Identifiable {
    case opened(sessionId: UUID, at: Date)
    case turn(ChatTurn)
    case proposal(ChangeProposal)

    var id: String {
        switch self {
        case .opened(let sessionId, _): "opened-\(sessionId.uuidString)"
        case .turn(let turn): turn.id.uuidString
        case .proposal(let proposal): "proposal-\(proposal.changeProposalId.uuidString)"
        }
    }
}

/// The thread laid out for the screen.
enum ThreadTranscript {
    /// Every session's opening followed by its turns and its proposals, oldest first, then the
    /// athlete's unsaved turn.
    static func rows(_ thread: ChatThread, tail: ChatTurn?) -> [ThreadRow] {
        let kept = thread.sessions.flatMap { session in
            [ThreadRow.opened(sessionId: session.sessionId, at: session.openedAt)] + interleaved(session)
        }
        return kept + (tail.map { [ThreadRow.turn($0)] } ?? [])
    }

    /// A session's turns with each proposal after the turns that came before it — the answer that
    /// proposed it is stored a moment before the proposal is, so the card follows the words about it.
    private static func interleaved(_ session: ThreadSession) -> [ThreadRow] {
        let proposals = session.proposals.sorted { $0.proposedAt < $1.proposedAt }
        let merged = session.transcript.reduce(into: (rows: [ThreadRow](), waiting: proposals[...])) { merged, message in
            let due = merged.waiting.prefix { proposal in message.createdAt.map { proposal.proposedAt < $0 } ?? false }
            merged.rows += due.map(ThreadRow.proposal) + [.turn(.said(message))]
            merged.waiting = merged.waiting.dropFirst(due.count)
        }
        return merged.rows + merged.waiting.map(ThreadRow.proposal)
    }

    /// The stored answer to `words`, once the thread holds one: the model's turn straight after the
    /// newest session's last words from the athlete, when those are `words` and are not among the
    /// turns `known` before they were sent — the same words asked earlier are another question.
    static func answer(to words: String, after known: Set<UUID>, in thread: ChatThread) -> ChatMessage? {
        guard let transcript = thread.sessions.last?.transcript,
              let asked = transcript.lastIndex(where: { ChatTurn.said($0).isFromAthlete }),
              transcript[asked].text == words,
              !known.contains(transcript[asked].chatMessageId),
              transcript.indices.contains(asked + 1)
        else { return nil }
        return transcript[asked + 1]
    }

    /// How a session's opening reads: "Today" or "Yesterday" and the time, then the weekday and
    /// date with the time, with the year only once it is not this one.
    static func openingLabel(_ opened: Date, now: Date, calendar: Calendar = .current) -> String {
        let time = opened.formatted(Date.FormatStyle(
            date: .omitted, time: .shortened, locale: calendar.locale ?? .current, calendar: calendar,
            timeZone: calendar.timeZone))
        if calendar.isDate(opened, inSameDayAs: now) { return "Today \(time)" }
        if let yesterday = calendar.date(byAdding: .day, value: -1, to: now),
           calendar.isDate(opened, inSameDayAs: yesterday) {
            return "Yesterday \(time)"
        }

        var style = Date.FormatStyle(locale: calendar.locale ?? .current, calendar: calendar, timeZone: calendar.timeZone)
            .weekday(.abbreviated).day().month(.abbreviated)
        if calendar.component(.year, from: opened) != calendar.component(.year, from: now) {
            style = style.year()
        }
        return "\(opened.formatted(style)), \(time)"
    }
}
