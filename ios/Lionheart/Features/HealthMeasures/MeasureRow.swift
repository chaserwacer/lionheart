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

    var body: some View {
        let comparison = MeasureComparison.of(days, endingOn: today)
        let weeks = WeekMean.of(days)

        HStack(alignment: .center, spacing: 12) {
            VStack(alignment: .leading, spacing: 2) {
                Text(metric.title)
                Text(detail(comparison))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Spacer(minLength: 8)
            if let comparison {
                VStack(alignment: .trailing, spacing: 2) {
                    Text(metric.text(comparison.recent))
                        .font(.body.monospacedDigit())
                    if let change = comparison.change {
                        Text(metric.change(change))
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Self.colour(of: metric.isImprovement(change)))
                    }
                }
            }
            if weeks.count >= 2 {
                Sparkline(points: Array(weeks.suffix(26)).map { MetricPoint(day: $0.weekStarting, value: metric.shown($0.mean)) })
                    .frame(width: 64, height: 28)
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityValue(accessibilityValue(comparison))
    }

    private func detail(_ comparison: MeasureComparison?) -> String {
        if let comparison {
            return comparison.before == nil
                ? "Last 4 weeks · \(Formatting.count(comparison.recentDays, "day"))"
                : "Last 4 weeks against the 12 before"
        }
        guard let last = days.last else { return "Not measured" }
        return "Last \(metric.text(last.mean)) · \(Formatting.date(last.day))"
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
