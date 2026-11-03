import Foundation

/// One figure on one day.
struct MetricPoint: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let value: Double

    var id: CalendarDay { day }
}

/// One recovery figure on the day being read, set against the athlete's own baseline.
///
/// The recent run of the figure travels with it, because the screen draws it as a sparkline beside
/// the number — a figure alone says what it is, the run says whether that is unusual.
struct MetricReading: Equatable, Sendable, Identifiable {
    let metric: RecoveryMetric
    let day: CalendarDay
    let value: Double
    /// `nil` when there are too few earlier days, or the figure is not compared to a baseline.
    let baseline: MetricBaseline?
    /// The figure across the baseline window and the day itself, oldest first; only days that
    /// carry it.
    let recent: [MetricPoint]

    var id: RecoveryMetric { metric }

    /// How far the day is from his baseline, in the figure's own unit.
    var deviation: Double? { baseline.map { value - $0.mean } }

    var standing: MetricStanding {
        guard let baseline else { return .noBaseline }
        return baseline.standing(of: value, polarity: metric.polarity)
    }
}

/// Everything the ring says about one day's recovery: Oura's readiness verdict, and the figures
/// beneath it, each against his baseline.
///
/// The day is the one the figures belong to, which on Today can be earlier than the day being
/// viewed — the ring may not have synced yet this morning — and the screen says so when it is.
struct RecoveryReading: Equatable, Sendable {
    let day: CalendarDay
    /// Oura's readiness on the day, with its band, where it issued one.
    let readiness: ReadinessReading?
    /// Readiness as a figure against his baseline, for the verdict's own comparison and sparkline.
    let readinessFigure: MetricReading?
    /// Every other figure the day carries, in the order the screen reads them.
    let figures: [MetricReading]

    var isEmpty: Bool { readiness == nil && figures.isEmpty }

    /// The figures, in the order a recovery is read: the nervous system, then the night, then the
    /// day.
    static let figureOrder: [RecoveryMetric] = [
        .hrv, .lowestHeartRate, .sleepDuration, .sleepScore, .respiratoryRate, .temperatureDeviation, .steps,
    ]

    /// Reads one day's recovery out of a window of days and nights that reaches at least
    /// `MetricBaseline.windowDays` before it. `nil` when the ring filed nothing for the day.
    ///
    /// On `today`, a figure that accrues through the day is shown as it stands, with no comparison
    /// and no run beside it: the morning's step count against 28 whole days would read as a bad
    /// day every morning.
    static func reading(
        for day: CalendarDay,
        days: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>],
        today: CalendarDay,
        calendar: Calendar = .current
    ) -> RecoveryReading? {
        let byDay = Dictionary(days.map { ($0.date, $0) }, uniquingKeysWith: { first, _ in first })
        let windowStart = day.adding(days: -MetricBaseline.windowDays, in: calendar)
        let windowDays = Set(byDay.keys.filter { $0 >= windowStart && $0 <= day })
            .union(nights.map(\.day).filter { $0 >= windowStart && $0 <= day })

        func figure(_ metric: RecoveryMetric) -> MetricReading? {
            let recent = windowDays.sorted().compactMap { date in
                metric.value(day: byDay[date], night: OuraSleepPeriod.mainSleep(on: date, in: nights))
                    .map { MetricPoint(day: date, value: $0) }
            }
            guard let onTheDay = recent.last, onTheDay.day == day else { return nil }
            let earlier = recent.dropLast().map(\.value)
            let isUnfinished = metric.accruesThroughTheDay && day >= today
            return MetricReading(
                metric: metric,
                day: day,
                value: onTheDay.value,
                baseline: metric.comparesToBaseline && !isUnfinished ? MetricBaseline.of(earlier) : nil,
                recent: isUnfinished ? [] : recent)
        }

        let readiness = byDay[day].flatMap { TodaySnapshot.latestReadiness(in: [$0]) }
        let figures = figureOrder.compactMap(figure)
        guard readiness != nil || !figures.isEmpty else { return nil }

        return RecoveryReading(
            day: day,
            readiness: readiness,
            readinessFigure: figure(.readiness),
            figures: figures)
    }

    /// The day Today reads its recovery from: the newest day in the window carrying a readiness,
    /// or failing that the newest day carrying anything — the morning before the ring has synced
    /// still shows last night's figures, labelled with their day.
    static func readingDay(
        onOrBefore day: CalendarDay, days: [OuraDay], nights: [OuraEvent<OuraSleepPeriod>]
    ) -> CalendarDay? {
        let candidates = days.filter { $0.date <= day }
        if let readiness = TodaySnapshot.latestReadiness(in: candidates) { return readiness.day }
        let nightDays = nights.map(\.day).filter { $0 <= day }
        return (candidates.map(\.date) + nightDays).max()
    }
}
