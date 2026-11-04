import Foundation

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
