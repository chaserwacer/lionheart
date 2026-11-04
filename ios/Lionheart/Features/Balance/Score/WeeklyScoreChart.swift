import Charts
import SwiftUI

/// The training score of each week ending on a Sunday, as quiet bars against the 90 that marks an
/// excellent week, with the week on screen in the accent.
///
/// A week whose score was read with a part unknown is drawn hollow-toned, so a number that could
/// only reach 89 never sits beside a whole one as if the two said the same.
struct WeeklyScoreChart: View {
    /// Oldest first.
    let weeks: [WeeklyScore]
    var highlighted: CalendarDay?

    /// The score that marks an excellent week, drawn as a rule.
    static let excellent = 90

    var body: some View {
        Chart {
            RuleMark(y: .value("Excellent", Self.excellent))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(Color(.tertiaryLabel))
            ForEach(weeks) { week in
                BarMark(
                    x: .value("Week", week.window.to.middayDate(), unit: .weekOfYear),
                    y: .value("Score", week.value))
                .foregroundStyle(colour(of: week))
            }
        }
        .chartYScale(domain: 0...100)
        .chartYAxis {
            AxisMarks(values: [0, 50, 90]) { _ in
                AxisGridLine()
                AxisValueLabel()
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
        .accessibilityLabel("Training score by week")
        .accessibilityValue(spoken)
    }

    private func colour(of week: WeeklyScore) -> Color {
        if week.window.to == highlighted { return Tokens.accent }
        return week.isPartial ? Color(.quaternarySystemFill) : Color(.systemFill)
    }

    private var spoken: String {
        let values = weeks.map(\.value)
        guard let best = values.max(), let last = weeks.last else { return "" }
        let median = values.sorted()[values.count / 2]
        return "Last week \(last.value). Median \(median), best \(best), over \(Formatting.count(weeks.count, "week"))."
    }
}
