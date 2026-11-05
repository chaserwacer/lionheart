import Charts
import SwiftUI

/// A daily figure over a span: each reading as a point, a line the eye follows, the span's mean as a
/// dashed rule, and a pressed day read back through `selection`.
///
/// General on purpose — bodyweight is its first user, and any per-day figure (a recovery figure, a
/// lift's estimate by session) draws the same way. The line is a smoothed series where the caller has
/// one, else the readings themselves: a measurement taken once a week has no seven-day mean, and
/// joining its points is the honest line.
struct DailyTrendChart: View {
    let points: [MetricPoint]
    /// A smoothed series to draw as the line, or `nil` to join the points.
    var line: [MetricPoint]?
    var mean: Double?
    /// The longest span shown, which decides how finely the axis labels days.
    var showsMonthsOnly = false
    @Binding var selection: Date?

    @ScaledMetric(relativeTo: .body) private var height: CGFloat = 220

    var body: some View {
        Chart {
            if let mean {
                RuleMark(y: .value("Mean", mean))
                    .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                    .foregroundStyle(Color(.tertiaryLabel))
            }
            ForEach(points) { point in
                PointMark(x: .value("Day", point.day.middayDate()), y: .value("Value", point.value))
                    .symbolSize(points.count > 120 ? 8 : 18)
                    .foregroundStyle(line == nil ? Tokens.accent : Tokens.iron.opacity(0.5))
            }
            ForEach(line ?? points) { point in
                LineMark(x: .value("Day", point.day.middayDate()), y: .value("Line", point.value))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 2))
                    .foregroundStyle(Tokens.accent)
            }
            if let selection {
                RuleMark(x: .value("Selected", selection))
                    .foregroundStyle(Color(.separator))
            }
        }
        .chartYScale(domain: .automatic(includesZero: false))
        .chartXSelection(value: $selection)
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let date = value.as(Date.self) {
                        Text(date, format: showsMonthsOnly
                             ? .dateTime.month(.abbreviated)
                             : .dateTime.month(.abbreviated).day())
                    }
                }
            }
        }
        .frame(height: height)
        .padding(.vertical, 8)
    }

    /// The reading nearest a pressed date.
    static func nearest(to date: Date?, in points: [MetricPoint]) -> MetricPoint? {
        guard let date else { return nil }
        return points.min {
            abs($0.day.middayDate().timeIntervalSince(date)) < abs($1.day.middayDate().timeIntervalSince(date))
        }
    }
}
