import Foundation

/// How consistently training happens: which days had a session, sessions per week, and the runs of
/// weeks that had at least one.
///
/// Consistency is the athlete's first goal and his own analysis names the measure: whether a week
/// happens at all, since when he trains he trains about three times a week and the failure is the
/// weeks that never start. So the reading is plain counts — no streak to protect, no target to
/// fall short of. A run is reported as a length and its dates, the way his analysis states its
/// 35-week baseline.
struct ConsistencyReading: Equatable, Sendable {
    /// Every week from the first session through this one, oldest first, empty weeks included.
    let weeks: [TrainingWeek]
    /// The days a performed session is dated on.
    let trainingDays: Set<CalendarDay>
    /// The run of weeks with a session that is still going: through this week, or through last
    /// week when this one has had nothing yet — a Monday morning has not broken anything.
    let currentRun: WeekRun?
    let longestRun: WeekRun?

    /// Consecutive weeks that each held at least one session.
    struct WeekRun: Equatable, Sendable {
        /// Monday of the first week and of the last.
        let firstWeek: CalendarDay
        let lastWeek: CalendarDay
        let weekCount: Int
    }

    /// One span of weeks read as figures.
    struct Window: Equatable, Sendable {
        let weeks: [TrainingWeek]
        let sessions: Int
        let weeksTrained: Int
        /// Sessions per week over the whole window, empty weeks included.
        let perWeek: Double
        /// The median sessions in the weeks that had any — the "when I train, I train three times"
        /// figure, which the empty weeks would otherwise hide.
        let medianInTrainedWeeks: Double?
    }

    func window(lastWeeks count: Int) -> Window {
        let shown = Array(weeks.suffix(count))
        let sessions = shown.reduce(0) { $0 + $1.sessionCount }
        let trained = shown.map(\.sessionCount).filter { $0 > 0 }.sorted()
        let median: Double? = trained.isEmpty ? nil : trained.count.isMultiple(of: 2)
            ? Double(trained[trained.count / 2 - 1] + trained[trained.count / 2]) / 2
            : Double(trained[trained.count / 2])
        return Window(
            weeks: shown,
            sessions: sessions,
            weeksTrained: trained.count,
            perWeek: shown.isEmpty ? 0 : Double(sessions) / Double(shown.count),
            medianInTrainedWeeks: median)
    }

    static func of(
        sessions: [TrainingSession], today: CalendarDay, calendar: Calendar = .current
    ) -> ConsistencyReading {
        let performed = sessions.filter { SessionLog.kind(of: $0, today: today).isPerformed }
        let weeks = TrainingTrends.weeks(of: performed, today: today, calendar: calendar)
        let runs = runs(in: weeks)

        return ConsistencyReading(
            weeks: weeks,
            trainingDays: Set(performed.map(\.date).filter { $0 <= today }),
            currentRun: current(of: runs, in: weeks),
            longestRun: runs.max { ($0.weekCount, $0.lastWeek) < ($1.weekCount, $1.lastWeek) })
    }

    /// Every maximal run of weeks with a session, oldest first. `weeks` is consecutive with its
    /// empty weeks included, so a run continues exactly when the week before it had a session.
    static func runs(in weeks: [TrainingWeek]) -> [WeekRun] {
        zip([nil] + weeks.map(Optional.some), weeks).reduce(into: [WeekRun]()) { runs, pair in
            let (previous, week) = pair
            guard week.sessionCount > 0 else { return }
            if let last = runs.last, (previous?.sessionCount ?? 0) > 0 {
                runs[runs.count - 1] = WeekRun(
                    firstWeek: last.firstWeek, lastWeek: week.start, weekCount: last.weekCount + 1)
            } else {
                runs.append(WeekRun(firstWeek: week.start, lastWeek: week.start, weekCount: 1))
            }
        }
    }

    /// The run ending this week, or last week while this one is still empty.
    private static func current(of runs: [WeekRun], in weeks: [TrainingWeek]) -> WeekRun? {
        guard let last = runs.last, let thisWeek = weeks.last else { return nil }
        if last.lastWeek == thisWeek.start { return last }
        let previous = weeks.dropLast().last
        return thisWeek.sessionCount == 0 && previous?.start == last.lastWeek ? last : nil
    }
}

/// A month laid out as a calendar page: Monday-first weeks, with blanks before the first and after
/// the last day so every row has seven cells.
struct CalendarMonth: Equatable, Sendable, Identifiable {
    /// The first of the month.
    let start: CalendarDay
    /// Rows of seven; `nil` is a blank cell outside the month.
    let weeks: [[CalendarDay?]]

    var id: CalendarDay { start }

    /// The months from the one holding `first` through the one holding `last`, newest first — the
    /// order a log is read in.
    static func months(
        from first: CalendarDay, through last: CalendarDay, calendar: Calendar = .current
    ) -> [CalendarMonth] {
        var starts: [CalendarDay] = []
        var cursor = CalendarDay(year: first.year, month: first.month, day: 1)
        let end = CalendarDay(year: last.year, month: last.month, day: 1)
        // Bounded by a decade of months so a mistyped year cannot build forever.
        while cursor <= end, starts.count < 120 {
            starts.append(cursor)
            cursor = cursor.month == 12
                ? CalendarDay(year: cursor.year + 1, month: 1, day: 1)
                : CalendarDay(year: cursor.year, month: cursor.month + 1, day: 1)
        }
        return starts.reversed().map { page(starting: $0, calendar: calendar) }
    }

    static func page(starting start: CalendarDay, calendar: Calendar = .current) -> CalendarMonth {
        let date = start.middayDate(in: calendar)
        let dayCount = calendar.range(of: .day, in: .month, for: date)?.count ?? 30
        // Monday is column zero whatever the device's own first weekday, matching how weeks are
        // counted everywhere else in the log.
        let weekday = calendar.component(.weekday, from: date)
        let leading = (weekday + 5) % 7
        let cells: [CalendarDay?] = Array(repeating: nil, count: leading)
            + (1...dayCount).map { CalendarDay(year: start.year, month: start.month, day: $0) }
        let padded = cells + Array(repeating: nil, count: (7 - cells.count % 7) % 7)
        return CalendarMonth(
            start: start,
            weeks: stride(from: 0, to: padded.count, by: 7).map { Array(padded[$0..<($0 + 7)]) })
    }
}
