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
    /// When it began, where it has begun — what tells two sessions on one day apart.
    let startedAt: Date?
    let kind: LoggedSessionKind
    /// The movements the session touched, named — or the lifecycle word when it touched none.
    let headline: String
    let facts: LoggedSessionFacts
    /// Each movement as a log-book line — its load and reps, or its top set — in the order done.
    let lines: [MovementDigest]
    /// What the sets came to: the count and the volume per unit.
    let totals: WorkoutTotals
    /// The movements the sets were performed on, which is what filtering the log by one reads.
    let movementIds: Set<UUID>
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

/// The session log, read as a diary: weeks, newest first, each holding the days that had a session.
///
/// Weeks rather than months because the athlete's first goal is measured in them — sessions a
/// week, and whether a week happens at all — so each week's head is that count, and opens the
/// week. Within a week the rows are days rather than sessions: a day is what he lived, and it opens
/// onto everything else that day held, the ring's night and the cardio among it. The grouping is
/// here and not in the view so that the section heads and the search both see the same reading.
struct SessionLog: Equatable, Sendable {
    /// Newest first; only weeks that hold a row.
    let weeks: [Week]

    /// One Monday-to-Sunday week and the days in it that hold a session, newest first.
    struct Week: Equatable, Sendable, Identifiable {
        /// Monday.
        let start: CalendarDay
        let days: [Day]

        var id: CalendarDay { start }
        var sessions: [LoggedSession] { days.flatMap(\.sessions) }
        /// The sessions that happened — what "sessions a week" counts.
        var performedCount: Int { sessions.filter(\.kind.isPerformed).count }
    }

    /// One day's sessions, in the order they happened.
    struct Day: Equatable, Sendable, Identifiable {
        let day: CalendarDay
        let sessions: [LoggedSession]

        var id: CalendarDay { day }
    }

    /// The log as a timeline: each week that holds a row, and between them each run of weeks that
    /// held none, said once rather than left out — a gap of two months is part of the record.
    enum Stretch: Equatable, Sendable, Identifiable {
        case week(Week)
        /// `count` weeks with nothing logged, the earliest beginning on `firstWeek`.
        case quiet(firstWeek: CalendarDay, count: Int)

        var id: String {
            switch self {
            case .week(let week): "week-\(week.start.wire)"
            case .quiet(let first, _): "quiet-\(first.wire)"
            }
        }
    }

    var isEmpty: Bool { weeks.allSatisfy(\.days.isEmpty) }
    var sessionCount: Int { weeks.reduce(0) { $0 + $1.sessions.count } }

    /// The weeks with the quiet runs between them, newest first.
    func stretches(calendar: Calendar = .current) -> [Stretch] {
        zip(weeks, weeks.dropFirst().map(Optional.some) + [nil]).flatMap { newer, older -> [Stretch] in
            guard let older else { return [.week(newer)] }
            let between = older.start.days(until: newer.start, in: calendar) / 7 - 1
            return between > 0
                ? [.week(newer), .quiet(firstWeek: older.start.adding(days: 7, in: calendar), count: between)]
                : [.week(newer)]
        }
    }

    /// The log with only the rows matching `query`, and days and weeks that kept nothing dropped.
    func filtered(by query: String) -> SessionLog {
        guard !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return self }
        return keeping { $0.matches(query) }
    }

    /// The log with only the sessions that performed every one of `movements`.
    ///
    /// By identity, never by name: a paused squat and a high-bar squat are different movements,
    /// and a filter for one must not bring in the other because their names share a word.
    func filtered(byMovements movements: Set<UUID>) -> SessionLog {
        guard !movements.isEmpty else { return self }
        return keeping { movements.isSubset(of: $0.movementIds) }
    }

    private func keeping(_ isKept: (LoggedSession) -> Bool) -> SessionLog {
        SessionLog(weeks: weeks.compactMap { week in
            let days = week.days.compactMap { day in
                let kept = day.sessions.filter(isKept)
                return kept.isEmpty ? nil : Day(day: day.day, sessions: kept)
            }
            return days.isEmpty ? nil : Week(start: week.start, days: days)
        })
    }
}

extension SessionLog {
    /// Reads the log out of one session window.
    ///
    /// Newest first across weeks and days — a log is read backwards from now — and in the order
    /// the server sent them within a day. `today` is injected rather than taken from the clock,
    /// which is what lets "missed" be tested.
    static func reading(
        sessions: [TrainingSession],
        catalogue: MovementCatalogue,
        today: CalendarDay,
        calendar: Calendar = .current
    ) -> SessionLog {
        let rows = sessions.map { row(for: $0, catalogue: catalogue, today: today) }
        let byDay = Dictionary(grouping: rows, by: \.day)
        let byWeek = Dictionary(grouping: byDay.keys) { TrainingTrends.weekStart(of: $0, calendar: calendar) ?? $0 }

        return SessionLog(weeks: byWeek.keys.sorted(by: >).map { start in
            Week(start: start, days: (byWeek[start] ?? []).sorted(by: >).map { day in
                Day(day: day, sessions: byDay[day] ?? [])
            })
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
            startedAt: session.startedAt,
            kind: kind,
            headline: headline,
            facts: facts(of: session),
            lines: MovementDigest.of(session.sets, catalogue: catalogue),
            totals: WorkoutTotals.of(entries: session.sets),
            movementIds: Set(session.sets.compactMap(\.movementDataId)),
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
