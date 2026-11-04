import Foundation

/// Where the Body tab can navigate.
///
/// One enum for the whole tab, declared outside any screen, because several screens push into it —
/// the tab's root, the Oura day list, a day, the cardio and movement screens — and the destination
/// is registered exactly once on the `NavigationStack`. Register two destinations for the same
/// value type and the later one silently wins, which is the failure this shape avoids.
enum BodyRoute: Hashable {
    case ouraDays
    case ouraDay(CalendarDay)
    /// The night's main sleep filed under this waking day.
    case night(CalendarDay)
    case cardio
    /// A session in the cardio report for that span, by its id.
    case cardioSession(String, TrendSpan)
    case habits
    case dayActivity(CalendarDay)
    case wellness
    case injuries
    case injury(UUID)
    case composition
    case bodyweight
    /// One recorded scan, by its id.
    case scan(UUID)
}
