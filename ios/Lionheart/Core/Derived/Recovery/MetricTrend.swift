import Foundation

/// How far back a trend reaches.
enum TrendSpan: Int, CaseIterable, Hashable, Sendable, Identifiable {
    case month = 30
    case quarter = 90
    case year = 365

    var id: Int { rawValue }
    var days: Int { rawValue }

    var label: String {
        switch self {
        case .month: "30D"
        case .quarter: "90D"
        case .year: "1Y"
        }
    }
}

/// Which trend a screen asked for: one figure, over one span, ending on one day.
struct MetricTrendKey: Hashable, Sendable {
    let metric: RecoveryMetric
    let span: TrendSpan
    let endingOn: CalendarDay

    /// The days to read for this trend, both ends included.
    func range(calendar: Calendar = .current) -> DayRange {
        DayRange(from: endingOn.adding(days: 1 - span.days, in: calendar), to: endingOn)
    }
}

/// One recovery figure over a span: every day that carries it, a seven-day mean through them, and
/// the summary figures a trend is read by.
///
/// The seven-day mean is what the eye should follow — a single night's HRV moves more than a week
/// of training does — and the daily points stay beneath it so the spread is visible too.
struct MetricTrend: Equatable, Sendable {
    let key: MetricTrendKey
    /// Oldest first; only days that carry the figure, so a gap is a gap rather than a zero.
    let points: [MetricPoint]
    /// The trailing seven-day mean on each day it can honestly be taken.
    let rollingMean: [MetricPoint]

    var mean: Double? {
        points.isEmpty ? nil : points.reduce(0) { $0 + $1.value } / Double(points.count)
    }

    var lowest: MetricPoint? { points.min { $0.value < $1.value } }
    var highest: MetricPoint? { points.max { $0.value < $1.value } }

    /// The days the trailing mean averages over, and how many of them must carry the figure for the
    /// mean to be drawn — fewer and one night would stand in for a week.
    static let rollingDays = 7
    static let rollingMinimum = 4

    static func reading(
        _ key: MetricTrendKey,
        days: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>],
        today: CalendarDay,
        calendar: Calendar = .current
    ) -> MetricTrend {
        // A figure still accruing today would be the trend's lowest point every morning.
        let range = key.range(calendar: calendar)
        let lastFinishedDay = key.metric.accruesThroughTheDay ? today.adding(days: -1, in: calendar) : range.to
        let byDay = Dictionary(days.map { ($0.date, $0) }, uniquingKeysWith: { first, _ in first })
        let mainSleeps = Dictionary(
            nights.filter(\.reading.isMainSleep).map { ($0.day, $0.reading) },
            uniquingKeysWith: { first, _ in first })

        let points = Set(byDay.keys).union(mainSleeps.keys)
            .filter { $0 >= range.from && $0 <= range.to && $0 <= lastFinishedDay }
            .sorted()
            .compactMap { day in
                key.metric.value(day: byDay[day], night: mainSleeps[day]).map { MetricPoint(day: day, value: $0) }
            }

        return MetricTrend(key: key, points: points, rollingMean: rollingMean(of: points, calendar: calendar))
    }

    /// The trailing mean at each point, over the points in the seven days ending on it.
    static func rollingMean(of points: [MetricPoint], calendar: Calendar = .current) -> [MetricPoint] {
        points.compactMap { point in
            let windowStart = point.day.adding(days: 1 - rollingDays, in: calendar)
            let inWindow = points.filter { $0.day >= windowStart && $0.day <= point.day }
            guard inWindow.count >= rollingMinimum else { return nil }
            return MetricPoint(day: point.day, value: inWindow.reduce(0) { $0 + $1.value } / Double(inWindow.count))
        }
    }
}
