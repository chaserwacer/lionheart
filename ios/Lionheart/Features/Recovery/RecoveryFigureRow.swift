import SwiftUI

/// One recovery figure as a table row: its name, the figure, how far it sits from the athlete's
/// baseline, and its last four weeks.
///
/// The columns have fixed, Dynamic-Type-scaled widths so the figures and deviations of every row
/// line up down the section, which is what lets the eye run down a column the way it reads a
/// table. From the first size above the default, where four columns no longer leave the name room,
/// the name moves above the figures rather than squeezing to an ellipsis.
struct RecoveryFigureRow: View {
    let reading: MetricReading

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .body) private var figureWidth: CGFloat = 80
    @ScaledMetric(relativeTo: .subheadline) private var deviationWidth: CGFloat = 56
    @ScaledMetric(relativeTo: .body) private var sparklineWidth: CGFloat = 52
    @ScaledMetric(relativeTo: .body) private var sparklineHeight: CGFloat = 22

    var body: some View {
        Group {
            if dynamicTypeSize >= .xLarge {
                VStack(alignment: .leading, spacing: 4) {
                    name
                    HStack(alignment: .center, spacing: 12) {
                        figure
                        deviation
                        Spacer(minLength: 8)
                        if !dynamicTypeSize.isAccessibilitySize {
                            sparkline.frame(width: sparklineWidth, height: sparklineHeight)
                        }
                    }
                }
            } else {
                HStack(spacing: 8) {
                    name.frame(maxWidth: .infinity, alignment: .leading)
                    figure.frame(width: figureWidth, alignment: .trailing)
                    deviation.frame(width: deviationWidth, alignment: .trailing)
                    sparkline.frame(width: sparklineWidth, height: sparklineHeight)
                }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(Formatting.metricTitle(reading.metric))
        .accessibilityValue(spoken)
    }

    /// The figure's name, marked as Oura's where it is one of Oura's scores rather than a
    /// measurement, so the two never read alike in one table.
    private var name: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(Formatting.metricName(reading.metric))
                .font(.subheadline)
                .lineLimit(1)
            if reading.metric.isOuraScore {
                Text("Oura's score")
                    .font(.caption2)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(1)
            }
        }
    }

    private var figure: some View {
        let parts = Formatting.figure(reading.value, of: reading.metric)
        return HStack(alignment: .firstTextBaseline, spacing: 2) {
            Text(parts.number)
                .font(.body.weight(.semibold).monospacedDigit())
            if let unit = parts.unit {
                Text(unit)
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .lineLimit(1)
    }

    @ViewBuilder
    private var deviation: some View {
        if let deviation = reading.deviation {
            Text(Formatting.deviation(deviation, of: reading.metric))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(standingColour)
                .lineLimit(1)
        } else {
            Text(" ").font(.subheadline)
        }
    }

    @ViewBuilder
    private var sparkline: some View {
        if reading.recent.count >= 2 {
            Sparkline(points: reading.recent, baseline: reading.baseline?.mean, mark: markColour)
        } else {
            Color.clear
        }
    }

    /// Colour only where the day is outside his normal spread in the worse direction — the one
    /// thing on the row worth the eye being pulled to.
    private var standingColour: Color {
        switch reading.standing {
        case .unfavourable: .orange
        case .typical, .favourable, .noBaseline: Color(.secondaryLabel)
        }
    }

    private var markColour: Color {
        switch reading.standing {
        case .unfavourable: .orange
        case .typical, .favourable, .noBaseline: Color(.label)
        }
    }

    private var spoken: String {
        let figure = Formatting.figureText(reading.value, of: reading.metric)
        guard let deviation = reading.deviation else { return figure }
        let standing = switch reading.standing {
        case .unfavourable: ", outside your usual range"
        case .favourable: ", better than usual"
        case .typical, .noBaseline: ""
        }
        return "\(figure), \(Formatting.deviation(deviation, of: reading.metric)) against your baseline\(standing)"
    }
}
