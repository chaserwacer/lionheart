import Foundation

/// Where the Body tab can navigate.
///
/// One enum for the whole tab, declared outside any screen, because two screens push into it —
/// the tab's root and the Oura day list — and the destination is registered exactly once on the
/// `NavigationStack`. Register two destinations for the same value type and the later one silently
/// wins, which is the failure this shape avoids.
enum BodyRoute: Hashable {
    case ouraDays
    case ouraDay(CalendarDay)
    case wellness
    case injuries
    case injury(UUID)
}
