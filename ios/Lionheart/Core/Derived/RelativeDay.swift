import Foundation

/// How a calendar day reads relative to today.
///
/// A case rather than a formatted string, so the view chooses the words and this stays testable
/// without a locale. "Yesterday" and "6 days ago" are different sentences, not one with a number
/// in it, which is why `.daysAgo` and `.yesterday` are both here.
enum RelativeDay: Equatable, Sendable {
    case today
    case yesterday
    case tomorrow
    case daysAgo(Int)
    case inDays(Int)
    /// Far enough away that a count stops helping and a date reads better.
    case distant(CalendarDay)

    /// Beyond this many days either side, a count is less useful than the date itself.
    static let countableSpan = 6

    static func of(_ day: CalendarDay, today: CalendarDay, calendar: Calendar = .current) -> RelativeDay {
        guard let difference = calendar.dateComponents(
            [.day],
            from: calendar.startOfDay(for: today.middayDate(in: calendar)),
            to: calendar.startOfDay(for: day.middayDate(in: calendar))).day
        else { return .distant(day) }

        switch difference {
        case 0: return .today
        case 1: return .tomorrow
        case -1: return .yesterday
        case 2...Self.countableSpan: return .inDays(difference)
        case (-Self.countableSpan)...(-2): return .daysAgo(-difference)
        default: return .distant(day)
        }
    }
}
