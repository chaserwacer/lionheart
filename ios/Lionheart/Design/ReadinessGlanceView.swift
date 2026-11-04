import Charts
import SwiftUI

/// The morning's readiness, drawn from a `TodayGlance` outside the app: the score large, Oura's
/// band, how it sits against his baseline, and its last weeks as a line.
///
/// One view for every place that shows the glance — the small widget, the medium widget's leading
/// half and the readiness intent's snippet — so they cannot come to disagree. It is compiled into
/// `LionheartWidgets` as well, which is why it reads nothing but the glance and system styles.
struct ReadinessGlanceView: View {
    let readiness: TodayGlance.Readiness
    /// The moment it is drawn, which names the day the score belongs to.
    let now: Date
    /// Whether to draw the line under the figures; the snippet and the small widget do.
    var showsTrend = true

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.secondary)
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text("\(readiness.score)")
                    .font(.largeTitle.weight(.semibold).monospacedDigit())
                    .contentTransition(.numericText(value: Double(readiness.score)))
                Text(readiness.band)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(readiness.asksForAttention ? AnyShapeStyle(.orange) : AnyShapeStyle(.secondary))
            }
            .minimumScaleFactor(0.8)
            .lineLimit(1)
            if let comparison = readiness.comparison {
                Text(comparison)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
            if showsTrend, readiness.recent.count >= 2 {
                GlanceTrend(values: readiness.recent, attention: readiness.asksForAttention)
                    .frame(maxHeight: .infinity)
                    .padding(.top, 4)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(title)
        .accessibilityValue(
            "\(readiness.score), \(readiness.band)\(readiness.comparison.map { ", \($0)" } ?? "")")
    }

    private var title: String {
        TodayGlance.isToday(readiness.day, at: now)
            ? "Readiness"
            : "Readiness · \(TodayGlance.dayName(readiness.day, at: now))"
    }
}

/// The score's recent run, as a line with the newest day marked — `Sparkline`'s look, for a list
/// of plain values, since the glance carries no days.
private struct GlanceTrend: View {
    let values: [Double]
    let attention: Bool

    var body: some View {
        Chart {
            ForEach(Array(values.enumerated()), id: \.offset) { index, value in
                LineMark(x: .value("Day", index), y: .value("Score", value))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let last = values.last {
                PointMark(x: .value("Day", values.count - 1), y: .value("Score", last))
                    .symbolSize(20)
                    .foregroundStyle(attention ? Color.orange : Color(.label))
            }
        }
        .chartXAxis(.hidden)
        .chartYAxis(.hidden)
        .chartLegend(.hidden)
        .chartYScale(domain: .automatic(includesZero: false))
        .accessibilityHidden(true)
    }
}
