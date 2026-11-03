import Foundation

/// Where the History tab's screens can navigate — and Today's too, for the screens that read the
/// log.
///
/// One enum declared outside any screen, because many screens push into it — the log, the records,
/// a movement's page, the focus list — and its destination is registered exactly once on each
/// `NavigationStack` that reaches these screens, through `HistoryDestination`. Register two
/// destinations for the same value type on one stack and the later one silently wins.
enum HistoryRoute: Hashable {
    case session(UUID)
    case records
    case recordSubject(RecordSubject)
    /// One record chain. Keyed by the whole `RecordKey`, unit included, so a pounds rep max and a
    /// kilograms one are two histories rather than one interleaved list.
    case recordHistory(RecordKey)
    case trends
    /// One movement's page: its history, its charts, its records and how often it is trained.
    case movement(UUID)
    /// Every movement the log holds.
    case movements
    /// Training days and sessions per week.
    case consistency
    /// The focus list.
    case focus
}
