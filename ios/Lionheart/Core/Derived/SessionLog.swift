import Foundation

/// What a session log row says about itself.
///
/// The lifecycle word is a case rather than a string because the log renders the four differently —
/// a skipped session is kept deliberately (the skip is data) and must not read as a completed one,
/// and a planned session whose day has passed is *missed*, which is a reading the server does not
/// make because it has no opinion about today.
enum LoggedSessionKind: Equatable, Sendable {
    case completed
    case inProgress
    case skipped
    /// Planned for a day that has gone by. The same server record as `.planned`, read against the
    /// athlete's today.
    case missed
    /// Planned for today or later. In the log because History shows the whole timeline, below the
    /// line where the past ends.
    case planned
    /// A lifecycle state this build has not heard of. It keeps its row: the envelope still carries
    /// a day and an id.
    case unrecognised(String)

    var label: String {
        switch self {
        case .completed: "Completed"
        case .inProgress: "In progress"
        case .skipped: "Skipped"
        case .missed: "Missed"
        case .planned: "Planned"
        case .unrecognised: "Unknown"
        }
    }

    /// The SF Symbol for the row's leading mark.
    var systemImage: String {
        switch self {
        case .completed: "checkmark.circle.fill"
        case .inProgress: "record.circle"
        case .skipped: "slash.circle"
        case .missed: "exclamationmark.circle"
        case .planned: "calendar"
        case .unrecognised: "questionmark.circle"
        }
    }

    /// Whether the row describes training that actually happened, which is what the trends read
    /// and what "sessions logged" counts.
    var isPerformed: Bool {
        switch self {
        case .completed, .inProgress: true
        case .skipped, .missed, .planned, .unrecognised: false
        }
    }
}

/// The countable facts a row has, which differ by lifecycle state.
///
/// A case per state rather than four optionals: a planned session has prescriptions and no
/// duration, a completed one has both, and a skipped one has neither — so "a duration of nil on a
/// planned session" is not a thing that needs representing.
enum LoggedSessionFacts: Equatable, Sendable {
    case performed(setCount: Int, duration: TimeInterval?, hasEffort: Bool)
    case inProgress(setCount: Int)
    case prescribed(count: Int)
    /// Skipped, or a state this build cannot read. Nothing to count.
    case none
}

/// One row of the session log, ready to render and ready to search.
///
/// `searchText` is built once here rather than recomputed per keystroke in the view: the log is the
/// screen's whole content, and matching a query against freshly-joined movement names for every row
/// on every character typed is the one place this screen could be made to feel slow.
struct LoggedSession: Equatable, Sendable, Identifiable {
    let sessionId: UUID
    let day: CalendarDay
    let kind: LoggedSessionKind
    /// The movements the session touched, named — or the lifecycle word when it touched none.
    let headline: String
    let facts: LoggedSessionFacts
    let notes: String
    /// Not `private`: `SessionLog.row(_:)` in the extension below is what builds it, and a private
    /// member would make the memberwise initialiser unreachable from there. It is still not for a
    /// view to read — `matches(_:)` is the only thing that should.
    let searchText: String

    var id: UUID { sessionId }
    var hasNotes: Bool { !notes.isEmpty }

    func matches(_ query: String) -> Bool {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return true }
        return searchText.localizedCaseInsensitiveContains(trimmed)
    }
}

/// The session log, grouped the way a reader scans it.
///
/// Months rather than a flat list: a year of training is a few hundred rows, and the only grouping
/// a lifter reads a log by is the month. The grouping is here and not in the view so that the
/// section headers and the search both see the same reading of the data.
struct SessionLog: Equatable, Sendable {
    let months: [Month]

    struct Month: Equatable, Sendable, Identifiable {
        /// The first day of the month, which is what a date formatter needs to name it.
        let start: CalendarDay
        let sessions: [LoggedSession]

        var id: CalendarDay { start }
    }

    var isEmpty: Bool { months.allSatisfy(\.sessions.isEmpty) }
    var sessionCount: Int { months.reduce(0) { $0 + $1.sessions.count } }

    /// The log with only the rows matching `query`, and months that kept nothing dropped.
    func filtered(by query: String) -> SessionLog {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return self }
        return SessionLog(months: months.compactMap { month in
            let kept = month.sessions.filter { $0.matches(query) }
            return kept.isEmpty ? nil : Month(start: month.start, sessions: kept)
        })
    }
}

extension SessionLog {
    /// Reads the log out of one session window.
    ///
    /// Newest first, within the month and across them: a log is read backwards from now. `today` is
    /// injected rather than taken from the clock, which is what lets "missed" be tested.
    static func reading(
        sessions: [TrainingSession],
        catalogue: MovementCatalogue,
        today: CalendarDay
    ) -> SessionLog {
        let rows = sessions
            .map { row(for: $0, catalogue: catalogue, today: today) }
            .sorted { $0.day > $1.day }

        let byMonth = Dictionary(grouping: rows) {
            CalendarDay(year: $0.day.year, month: $0.day.month, day: 1)
        }

        return SessionLog(months: byMonth.keys.sorted(by: >).map { start in
            Month(start: start, sessions: byMonth[start] ?? [])
        })
    }

    static func row(
        for session: TrainingSession, catalogue: MovementCatalogue, today: CalendarDay
    ) -> LoggedSession {
        let kind = kind(of: session, today: today)
        let movements = movementNames(in: session, catalogue: catalogue)
        let headline = movements.isEmpty ? kind.label : movements.joined(separator: ", ")
        let notes = session.envelope.notes.trimmingCharacters(in: .whitespacesAndNewlines)

        return LoggedSession(
            sessionId: session.id,
            day: session.date,
            kind: kind,
            headline: headline,
            facts: facts(of: session),
            notes: notes,
            searchText: ([headline, notes, kind.label] + movements).joined(separator: " "))
    }

    /// The lifecycle state read against the athlete's today.
    static func kind(of session: TrainingSession, today: CalendarDay) -> LoggedSessionKind {
        switch session {
        case .completed: .completed
        case .active: .inProgress
        case .skipped: .skipped
        case .planned(let planned): planned.plannedFor < today ? .missed : .planned
        case .unrecognised(let type, _): .unrecognised(type)
        }
    }

    /// The movements a session touched, named, in the order they were first touched.
    ///
    /// First-touch order rather than sorted: that is the order the session happened in, and a
    /// lifter reading "Bench press, Chin-ups" expects the bench press first because that is what
    /// they did first.
    static func movementNames(
        in session: TrainingSession, catalogue: MovementCatalogue
    ) -> [String] {
        let ids = session.sets.compactMap(\.movementDataId) + prescribedMovementIds(in: session)
        var seen: Set<UUID> = []
        return ids
            .filter { seen.insert($0).inserted }
            .map { catalogue.name(for: $0) }
    }

    private static func prescribedMovementIds(in session: TrainingSession) -> [UUID] {
        switch session {
        case .planned(let planned): planned.prescriptions.compactMap(\.movementDataId)
        case .active, .completed, .skipped, .unrecognised: []
        }
    }

    /// The countable facts under the headline. The set and movement counts come from
    /// `SessionSummary`, which is the one reading of a session used on Today as well, so the two
    /// screens cannot report different numbers for the same session.
    static func facts(of session: TrainingSession) -> LoggedSessionFacts {
        switch session {
        case .completed(let completed):
            let summary = SessionSummary.of(completed)
            return .performed(
                setCount: summary.setCount, duration: summary.duration, hasEffort: summary.hasEffort)
        case .active(let active):
            return .inProgress(setCount: active.sets.count)
        case .planned(let planned):
            return .prescribed(count: planned.prescriptions.count)
        case .skipped, .unrecognised:
            return .none
        }
    }
}
