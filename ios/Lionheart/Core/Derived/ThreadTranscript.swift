import Foundation

/// Where the thread's one exchange stands. Apart from the transcript because the two vary
/// independently: a thread recalled offline can hold a question that failed to send, and a reply
/// can be awaited over a transcript that is not current.
enum ThreadExchange: Equatable, Sendable {
    case idle
    /// A question is out, on screen but not yet kept by the server.
    case awaitingReply(ChatTurn)
    /// The last question failed and the server kept nothing of it; it goes back to the composer.
    case refused(ChatTurn)

    var isAwaitingReply: Bool {
        switch self {
        case .awaitingReply: true
        case .idle, .refused: false
        }
    }

    /// The athlete's own turn still on screen, below everything the server kept.
    var tail: ChatTurn? {
        switch self {
        case .idle: nil
        case .awaitingReply(let turn), .refused(let turn): turn
        }
    }

    var unsentText: String? {
        switch self {
        case .refused(let turn): turn.text
        case .idle, .awaitingReply: nil
        }
    }
}

/// One row of the thread as it is drawn: a session's opening, or a turn.
enum ThreadRow: Equatable, Sendable, Identifiable {
    case opened(sessionId: UUID, at: Date)
    case turn(ChatTurn)

    var id: String {
        switch self {
        case .opened(let sessionId, _): "opened-\(sessionId.uuidString)"
        case .turn(let turn): turn.id.uuidString
        }
    }
}

/// The thread laid out for the screen.
enum ThreadTranscript {
    /// Every session's opening followed by its turns, oldest first, then the athlete's unsaved turn.
    static func rows(_ thread: ChatThread, tail: ChatTurn?) -> [ThreadRow] {
        let kept = thread.sessions.flatMap { session in
            [ThreadRow.opened(sessionId: session.sessionId, at: session.openedAt)]
                + session.transcript.map { ThreadRow.turn(.said($0)) }
        }
        return kept + (tail.map { [ThreadRow.turn($0)] } ?? [])
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
