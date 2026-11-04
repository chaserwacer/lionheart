import Foundation

/// One Monday-to-Sunday week as History's week page reads it, apart from the logged training: the cardio by
/// zone, the ring's week in means, and the athlete's own ratings.
///
/// Like `DiaryDay`, the logged training is read from the log beside it (`WeekTraining`).
struct DiaryWeek: Equatable, Sendable {
    /// Monday.
    let start: CalendarDay
    /// The ring's days in the week, oldest first.
    let ouraDays: [OuraDay]
    /// The week's main sleeps, by waking day.
    let nights: [OuraEvent<OuraSleepPeriod>]
    /// The server's tally of the week's cardio, or `nil` when the report did not cover it.
    let cardio: CardioWeek?
    /// The cardio sessions in the week, oldest first.
    let cardioSessions: [CardioSession]
    let maxHeartRate: MaxHeartRate?
    /// His ratings of the week's days, oldest first.
    let wellness: [WellnessState]

    /// The seven days, Monday first.
    var days: [CalendarDay] { (0..<7).map { start.adding(days: $0) } }

    /// The figures a week is read by, in reading order: Oura's two scores worth averaging, then the
    /// measurements. Activity's score is left out: it sits in the high nineties whatever the week
    /// held, so its mean says nothing.
    static let figures: [RecoveryMetric] = [
        .readiness, .sleepScore, .hrv, .lowestHeartRate, .sleepDuration, .steps,
    ]

    /// A figure's mean over the days that carry it.
    struct Mean: Equatable, Sendable {
        let metric: RecoveryMetric
        let value: Double
        /// How many of the week's days carried the figure.
        let days: Int
    }

    /// The week's mean of each figure the ring carried on at least one day. Today's steps are not
    /// counted until today is over, for the reason Today does not compare them.
    func means(today: CalendarDay) -> [Mean] {
        Self.figures.compactMap { metric in
            let values = days.compactMap { day -> Double? in
                guard !(metric.accruesThroughTheDay && day >= today) else { return nil }
                return metric.value(
                    day: ouraDays.first { $0.date == day },
                    night: nights.first { $0.day == day }?.reading)
            }
            guard !values.isEmpty else { return nil }
            return Mean(metric: metric, value: values.reduce(0, +) / Double(values.count), days: values.count)
        }
    }

    /// The mean of his overall wellness ratings, where he logged any.
    var wellnessMean: Double? {
        wellness.isEmpty ? nil : wellness.map(\.overallScore).reduce(0, +) / Double(wellness.count)
    }

    static func reading(
        weekStarting start: CalendarDay,
        ouraDays: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>],
        cardio: CardioReport,
        wellness: [WellnessState],
        calendar: Calendar = .current
    ) -> DiaryWeek {
        let end = start.adding(days: 6, in: calendar)
        let inWeek = { (day: CalendarDay) in day >= start && day <= end }
        return DiaryWeek(
            start: start,
            ouraDays: ouraDays.filter { inWeek($0.date) }.sorted { $0.date < $1.date },
            nights: nights.filter { inWeek($0.day) && $0.reading.isMainSleep },
            cardio: cardio.weeks.first { $0.weekStarting == start },
            cardioSessions: cardio.sessions
                .filter { inWeek(CalendarDay($0.start, in: calendar)) }
                .sorted { $0.start < $1.start },
            maxHeartRate: cardio.maxHeartRate,
            wellness: wellness.filter { inWeek($0.date) }.sorted { $0.date < $1.date })
    }
}

/// A week's logged training, session by session, from the log — lifting and distance work.
///
/// Sets and volume per unit, warm-ups left out, by the same arithmetic as every session total in
/// the app (`WorkoutTotals`), so the week's sum is the sum of the rows above it.
struct WeekTraining: Equatable, Sendable {
    struct Session: Equatable, Sendable, Identifiable {
        let sessionId: UUID
        let day: CalendarDay
        let totals: WorkoutTotals
        let duration: TimeInterval?

        var id: UUID { sessionId }
    }

    /// Oldest first.
    let sessions: [Session]
    let totals: WorkoutTotals

    var isEmpty: Bool { sessions.isEmpty }

    /// The sessions performed in the week starting `start`.
    static func of(
        _ sessions: [TrainingSession], weekStarting start: CalendarDay, today: CalendarDay,
        calendar: Calendar = .current
    ) -> WeekTraining {
        let end = start.adding(days: 6, in: calendar)
        let performed = sessions
            .filter { $0.date >= start && $0.date <= end && SessionLog.kind(of: $0, today: today).isPerformed }
            .sorted { ($0.date, $0.startedAt ?? .distantPast) < ($1.date, $1.startedAt ?? .distantPast) }
        return WeekTraining(
            sessions: performed.map { session in
                Session(
                    sessionId: session.id,
                    day: session.date,
                    totals: WorkoutTotals.of(entries: session.sets),
                    duration: session.completedCase.flatMap(\.period.duration))
            },
            totals: WorkoutTotals.of(entries: performed.flatMap(\.sets)))
    }
}
