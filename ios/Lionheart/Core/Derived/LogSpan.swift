import Foundation

/// How far back a reading of the training log reaches: a quarter, half a year, a year, or all of it.
///
/// Its own type rather than `TrendSpan`, which reads recovery in days: the log is read in weeks,
/// and it reaches back past a year once a history is imported, so it needs an "all".
enum LogSpan: CaseIterable, Hashable, Sendable, Identifiable {
    case quarter
    case half
    case year
    case all

    var id: Self { self }

    var label: String {
        switch self {
        case .quarter: "3M"
        case .half: "6M"
        case .year: "1Y"
        case .all: "All"
        }
    }

    /// The span in weeks, given how many weeks the log holds.
    func weeks(of available: Int) -> Int {
        switch self {
        case .quarter: 13
        case .half: 26
        case .year: 52
        case .all: max(available, 1)
        }
    }

    /// The first day the span covers, ending today — `nil` for all of it.
    func start(endingOn today: CalendarDay, calendar: Calendar = .current) -> CalendarDay? {
        switch self {
        case .quarter, .half, .year: today.adding(days: -weeks(of: 0) * 7 + 1, in: calendar)
        case .all: nil
        }
    }

    /// Whether the points are dense enough to want smaller marks.
    var isLong: Bool {
        switch self {
        case .quarter, .half: false
        case .year, .all: true
        }
    }
}
