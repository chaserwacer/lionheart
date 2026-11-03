import Charts
import SwiftUI

/// A figure's recent run, drawn small beside the number: the line, the athlete's baseline as a
/// dashed rule, and the day being read as the one marked point.
///
/// Built on Swift Charts with every axis, grid and legend removed — the stock chart is the right
/// renderer, but no stock view is a sparkline. It is decoration for sighted readers of a row that
/// already states its figure and deviation, so it is hidden from VoiceOver rather than read as a
/// second, vaguer account of the same numbers.
struct Sparkline: View {
    let points: [MetricPoint]
    var baseline: Double?
    /// The colour of the marked point, which carries the row's standing.
    var mark: Color = Color(.label)

    var body: some View {
        Chart {
            if let baseline {
                RuleMark(y: .value("Baseline", baseline))
                    .lineStyle(StrokeStyle(lineWidth: 1, dash: [2, 2]))
                    .foregroundStyle(Color(.tertiaryLabel))
            }
            ForEach(points) { point in
                LineMark(x: .value("Day", point.day.middayDate()), y: .value("Value", point.value))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let last = points.last {
                PointMark(x: .value("Day", last.day.middayDate()), y: .value("Value", last.value))
                    .symbolSize(24)
                    .foregroundStyle(mark)
            }
        }
        .chartXAxis(.hidden)
        .chartYAxis(.hidden)
        .chartLegend(.hidden)
        .chartYScale(domain: .automatic(includesZero: false))
        .accessibilityHidden(true)
    }
}
