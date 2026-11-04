import Charts
import SwiftUI

/// The training score of each week ending on a Sunday, or one part's points, as quiet bars, with
/// the seven days to today as a rule in the accent.
///
/// The whole score is drawn against the 90 that marks an excellent week; a part against its own
/// points. A week whose score was read with a part unknown is drawn pale, so a number that could
/// only reach 89 never sits beside a whole one as if the two said the same; a part unknown that
/// week has no bar at all.
struct WeeklyScoreChart: View {
    /// Oldest first.
    let weeks: [WeeklyScore]
    /// The part drawn, or the whole score when `nil`.
    var part: ScorePart?
    /// The top of the scale: 100 for the whole score, the part's points otherwise.
    var outOf: Double = 100
    /// The seven days to today, drawn as a rule in the accent: it rarely ends on a Sunday, so no
    /// bar is it, and colouring the last bar would invite reading that week as this one.
    var current: Double?

    /// The score that marks an excellent week, drawn as a rule.
    static let excellent = 90.0

    var body: some View {
        Chart {
            RuleMark(y: .value("Mark", part == nil ? Self.excellent : outOf))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(Color(.tertiaryLabel))
            if let current {
                RuleMark(y: .value("Seven days to today", current))
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Tokens.accent)
            }
            ForEach(weeks) { week in
                if let value = week.points(of: part) {
                    BarMark(
                        x: .value("Week", week.window.to.middayDate(), unit: .weekOfYear),
                        y: .value("Points", value))
                    .foregroundStyle(colour(of: week))
                }
            }
        }
        .chartYScale(domain: 0...outOf)
        .chartYAxis {
            AxisMarks(values: part == nil ? [0, 50, Self.excellent] : [0, outOf / 2, outOf]) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let number = value.as(Double.self) { Text(ScorePoints.text(number)) }
                }
            }
        }
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let date = value.as(Date.self) {
                        Text(date, format: .dateTime.month(.abbreviated).day())
                    }
                }
            }
        }
        .accessibilityLabel(part.map { "\($0.name) points by week" } ?? "Training score by week")
        .accessibilityValue(spoken)
    }

    private func colour(of week: WeeklyScore) -> Color {
        part == nil && week.isPartial ? Color(.quaternarySystemFill) : Color(.systemFill)
    }

    private var spoken: String {
        let values = weeks.compactMap { $0.points(of: part) }
        guard let best = values.max(), let last = weeks.last?.points(of: part) else { return "" }
        let median = values.sorted()[values.count / 2]
        let now = current.map { "Seven days to today \(ScorePoints.text($0)). " } ?? ""
        return "\(now)Last week \(ScorePoints.text(last)). Median \(ScorePoints.text(median)), best \(ScorePoints.text(best)), over \(Formatting.count(values.count, "week"))."
    }
}
