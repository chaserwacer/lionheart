import Foundation

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
