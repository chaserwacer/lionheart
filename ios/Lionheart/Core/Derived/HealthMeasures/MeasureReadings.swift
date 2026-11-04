import Foundation

/// Every Apple Health figure the server keeps, as the screens read them.
struct HealthMeasureHistory: Equatable, Sendable {
    let series: [HealthMeasureSeries]

    /// The days of a figure as one device measured it, oldest first; empty where it measured none.
    func days(_ metric: HealthMetric, by device: MeasuringDevice) -> [MeasuredDay] {
        series.first { $0.metric == metric && $0.device == device }?.days ?? []
    }

    /// The device that measured a figure on the most days, with its days — the one series a screen
    /// draws for it. One device, never a blend: the watch and the phone sit at different places on
    /// the body and read gait differently.
    func principal(_ metric: HealthMetric) -> (device: MeasuringDevice, days: [MeasuredDay])? {
        series
            .filter { $0.metric == metric && !$0.days.isEmpty }
            .max { $0.days.count < $1.days.count }
            .map { ($0.device, $0.days) }
    }

    var isEmpty: Bool { series.allSatisfy(\.days.isEmpty) }
}

/// A figure's level now against its level before: the mean of the last four weeks against the
/// twelve before them, ending on a given day.
///
/// Four weeks because gait moves over weeks — a few slow days of walking in boots are not a
/// change — and twelve before so the comparison is with a settled level rather than last month.
struct MeasureComparison: Equatable, Sendable {
    let recent: Double
    let before: Double?
    /// Days with a reading in the recent window.
    let recentDays: Int

    var change: Double? { before.map { recent - $0 } }

    static let recentDaysSpan = 28
    static let beforeDaysSpan = 84

    /// `nil` when nothing was measured in the last four weeks up to `endingOn`.
    static func of(_ days: [MeasuredDay], endingOn end: CalendarDay) -> MeasureComparison? {
        let recentFrom = end.adding(days: 1 - recentDaysSpan)
        let recent = days.filter { $0.day >= recentFrom && $0.day <= end }
        guard !recent.isEmpty else { return nil }
        let before = days.filter { $0.day >= recentFrom.adding(days: -beforeDaysSpan) && $0.day < recentFrom }
        return MeasureComparison(recent: mean(recent), before: before.isEmpty ? nil : mean(before), recentDays: recent.count)
    }

    private static func mean(_ days: [MeasuredDay]) -> Double {
        days.map(\.mean).reduce(0, +) / Double(days.count)
    }
}

/// A week or a calendar month of a figure: the mean of its days.
///
/// What a trend over years draws, rather than its days: six years of daily walking speed is two
/// thousand points of mostly noise. A week within a year, a month beyond one.
struct PeriodMean: Equatable, Sendable, Identifiable {
    /// The period's first day: a Monday, or the first of a month.
    let start: CalendarDay
    let mean: Double
    let days: Int

    var id: CalendarDay { start }

    /// Monday-to-Sunday weeks with any reading, oldest first.
    static func weekly(_ days: [MeasuredDay], calendar: Calendar = .current) -> [PeriodMean] {
        grouped(days) { weekStart(of: $0, calendar: calendar) }
    }

    /// Calendar months with any reading, oldest first.
    static func monthly(_ days: [MeasuredDay]) -> [PeriodMean] {
        grouped(days) { CalendarDay(year: $0.year, month: $0.month, day: 1) }
    }

    /// The periods split where too many are missing, so a line is drawn through what was measured
    /// and not across a year the device was not worn — the watch's history has one between two
    /// watches. Periods up to `bridging` days apart stay joined.
    static func runs(_ periods: [PeriodMean], bridging: Int, calendar: Calendar = .current) -> [[PeriodMean]] {
        periods.reduce(into: [[PeriodMean]]()) { runs, period in
            if let last = runs.last?.last, last.start.days(until: period.start, in: calendar) <= bridging {
                runs[runs.count - 1].append(period)
            } else {
                runs.append([period])
            }
        }
    }

    static func weekStart(of day: CalendarDay, calendar: Calendar = .current) -> CalendarDay {
        // Monday is weekday 2 in the Gregorian numbering whatever the locale's first day.
        let weekday = calendar.component(.weekday, from: day.middayDate(in: calendar))
        return day.adding(days: -((weekday + 5) % 7), in: calendar)
    }

    private static func grouped(_ days: [MeasuredDay], by start: (CalendarDay) -> CalendarDay) -> [PeriodMean] {
        Dictionary(grouping: days) { start($0.day) }
            .map { first, held in
                PeriodMean(start: first, mean: held.map(\.mean).reduce(0, +) / Double(held.count), days: held.count)
            }
            .sorted { $0.start < $1.start }
    }
}

/// Gait before an injury against gait after it — asymmetry and double support, the two figures
/// that rise when a leg is hurt: the body shortens its time on the sore side and keeps both feet
/// down for longer.
struct InjuryGait: Equatable, Sendable {
    let metric: HealthMetric
    /// The mean over the eight weeks before the injury, if anything was measured then.
    let before: Double?
    /// The mean over the four weeks from the injury — or up to today, for one newer than that.
    let after: Double?
    /// The mean over the last four weeks, for an injury older than that.
    let recent: Double?
    /// The days from eight weeks before the injury to eight after, for the chart.
    let around: [MeasuredDay]

    static let beforeDays = 56
    static let afterDays = 28

    static func of(_ metric: HealthMetric, _ days: [MeasuredDay], injuredOn injury: CalendarDay, today: CalendarDay) -> InjuryGait {
        func mean(from: CalendarDay, through: CalendarDay) -> Double? {
            let held = days.filter { $0.day >= from && $0.day <= through }
            return held.isEmpty ? nil : held.map(\.mean).reduce(0, +) / Double(held.count)
        }
        let afterEnd = min(injury.adding(days: afterDays - 1), today)
        let recentFrom = today.adding(days: 1 - afterDays)

        return InjuryGait(
            metric: metric,
            before: mean(from: injury.adding(days: -beforeDays), through: injury.adding(days: -1)),
            after: mean(from: injury, through: afterEnd),
            recent: recentFrom > afterEnd ? mean(from: recentFrom, through: today) : nil,
            around: days.filter { $0.day >= injury.adding(days: -beforeDays) && $0.day <= injury.adding(days: beforeDays) })
    }

    /// After against before, in the figure's own unit; `nil` without both.
    var change: Double? {
        guard let before, let after else { return nil }
        return after - before
    }

    var hasReadings: Bool { before != nil || after != nil || recent != nil }
}
