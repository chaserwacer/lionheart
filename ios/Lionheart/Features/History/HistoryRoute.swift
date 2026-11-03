import Foundation

/// Where the History tab can navigate.
///
/// One enum for the whole tab, declared outside any screen, because three screens push into it —
/// the log, the records list and a movement's records — and the destination is registered exactly
/// once on the `NavigationStack`. Register two destinations for the same value type and the later
/// one silently wins.
enum HistoryRoute: Hashable {
    case session(UUID)
    case records
    case recordSubject(RecordSubject)
    /// One record chain. Keyed by the whole `RecordKey`, unit included, so a pounds rep max and a
    /// kilograms one are two histories rather than one interleaved list.
    case recordHistory(RecordKey)
    case trends
}
