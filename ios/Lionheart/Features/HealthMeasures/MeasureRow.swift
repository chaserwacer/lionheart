import SwiftUI

/// One Apple Health figure as a row: its name, its level, how that has moved, and its recent run
/// drawn small.
///
/// The level is the last four weeks' mean against the twelve before (`MeasureComparison`) where it
/// was measured recently; a figure that stopped — the watch's, since it was last worn — shows its
/// last reading and when, so an old number never reads as today's.
struct MeasureRow: View {
    let metric: HealthMetric
    let days: [MeasuredDay]
    let today: CalendarDay

    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        let comparison = MeasureComparison.of(days, endingOn: today)

        Group {
            // At accessibility sizes the figure goes under the name and the sparkline goes: three
            // columns of large type leave none of them room.
            if typeSize.isAccessibilitySize {
                VStack(alignment: .leading, spacing: 4) {
                    name(comparison)
                    figure(comparison, alignment: .leading)
                }
            } else {
                HStack(alignment: .center, spacing: 12) {
                    name(comparison)
                    Spacer(minLength: 8)
                    figure(comparison, alignment: .trailing)
                    sparkline
                }
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityValue(accessibilityValue(comparison))
    }

    private func name(_ comparison: MeasureComparison?) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(metric.title)
            if let lapsed = lapsed(comparison) {
                Text(lapsed)
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    @ViewBuilder
    private func figure(_ comparison: MeasureComparison?, alignment: HorizontalAlignment) -> some View {
        if let comparison {
            VStack(alignment: alignment, spacing: 2) {
                Text(metric.text(comparison.recent))
                    .font(.body.monospacedDigit())
                if let change = comparison.change {
                    Text(metric.change(change))
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Self.colour(of: metric.isImprovement(change)))
                }
            }
        }
    }

    @ViewBuilder
    private var sparkline: some View {
        let weeks = PeriodMean.weekly(days).suffix(26)
        if weeks.count >= 2 {
            Sparkline(points: weeks.map { MetricPoint(day: $0.start, value: metric.shown($0.mean)) })
                .frame(width: 64, height: 28)
        }
    }

    /// For a figure not measured in the last four weeks, its last reading and when; nothing for a
    /// current one, whose level the section's footer explains.
    private func lapsed(_ comparison: MeasureComparison?) -> String? {
        guard comparison == nil, let last = days.last else { return nil }
        return "Last \(metric.text(last.mean)) · \(Formatting.recordDate(last.day.middayDate()))"
    }

    private func accessibilityValue(_ comparison: MeasureComparison?) -> String {
        guard let comparison else { return "" }
        let change = comparison.change.map { ", \(metric.change($0)) against the 12 weeks before" } ?? ""
        return "\(metric.text(comparison.recent))\(change)"
    }

    static func colour(of improvement: Bool?) -> Color {
        switch improvement {
        case true?: .green
        case false?: .orange
        case nil: Color(.secondaryLabel)
        }
    }
}
