import Foundation

/// Where this week's score sits among the weeks before it: their median and their best.
///
/// Read over the weeks the trend chart draws, the one on screen included, so "best 72" and the
/// tallest bar are the same week.
struct ScoreTrend: Equatable, Sendable {
    let median: Int
    let best: Int
    let weeks: Int

    /// `nil` with fewer than four weeks, when a median says nothing.
    static func of(_ weeks: [WeeklyScore]) -> ScoreTrend? {
        let values = weeks.map(\.value).sorted()
        guard values.count >= 4, let best = values.last else { return nil }
        return ScoreTrend(median: values[values.count / 2], best: best, weeks: values.count)
    }
}
