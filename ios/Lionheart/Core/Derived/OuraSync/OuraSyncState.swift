import Foundation

/// How far the stored Oura data reaches.
///
/// A case for "nothing stored" rather than a `nil` day, because the two read differently — an
/// account with no Oura data at all is set up differently from one a few days behind — and the sync
/// plans a different range for each.
enum OuraCoverage: Equatable, Sendable {
    case nothingStored
    /// The newest day any collection covers.
    case through(CalendarDay)
}

/// Where the Oura sync stands, as the sync control shows it.
///
/// The coverage travels with every case once it is known, because the control always says how far
/// the data reaches — "through 1 Aug" stays true while a sync is out and after one fails.
enum OuraSyncState: Equatable, Sendable {
    /// The coverage has not been read yet.
    case unknown
    case idle(OuraCoverage)
    /// A sync is out for `range`.
    case syncing(DayRange, from: OuraCoverage)
    /// The last sync finished; `coverage` is what it left behind.
    case synced(OuraSyncReport, coverage: OuraCoverage)
    /// The last sync, or the read of the coverage before it, failed. The coverage is whatever was
    /// known before the attempt.
    case failed(OuraSyncFailure, coverage: OuraCoverage?)

    var coverage: OuraCoverage? {
        switch self {
        case .unknown: nil
        case .idle(let coverage), .syncing(_, let coverage), .synced(_, let coverage): coverage
        case .failed(_, let coverage): coverage
        }
    }

    var isSyncing: Bool {
        switch self {
        case .syncing: true
        case .unknown, .idle, .synced, .failed: false
        }
    }
}
