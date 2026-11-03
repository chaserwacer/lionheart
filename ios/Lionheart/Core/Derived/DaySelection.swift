import Foundation

/// Which day the Today tab is showing.
///
/// `.today` is a case rather than a stored date, because it has to follow the clock: an app left
/// open across midnight should show the new day on its next refresh, not stay pinned to the one it
/// was opened on. A past day is fixed. There is no future case — a day that has not happened has
/// no readiness, no sleep and no training to read.
enum DaySelection: Hashable, Sendable {
    case today
    case past(CalendarDay)

    /// The calendar day this selection means, given today's.
    func day(today: CalendarDay) -> CalendarDay {
        switch self {
        case .today: today
        case .past(let day): min(day, today)
        }
    }

    /// The selection for `day`: today, or anything after it, is `.today`.
    static func of(_ day: CalendarDay, today: CalendarDay) -> DaySelection {
        day >= today ? .today : .past(day)
    }

    /// One step through the days, never past today.
    func stepped(by days: Int, today: CalendarDay, calendar: Calendar = .current) -> DaySelection {
        .of(day(today: today).adding(days: days, in: calendar), today: today)
    }

    var isToday: Bool {
        switch self {
        case .today: true
        case .past: false
        }
    }
}
