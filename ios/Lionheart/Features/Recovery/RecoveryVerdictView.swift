import SwiftUI

/// The day's verdict: Oura's readiness, large, with its band and how it sits against the athlete's
/// own baseline, and the last four weeks of it drawn beside.
///
/// The number carries the weight and everything else is quieter, so the row reads in one glance
/// and then in detail. The band is Oura's — Lionheart does not invent its own judgement of
/// recovery — and its colour is the one place on the screen that colour carries a verdict.
struct RecoveryVerdictView: View {
    let readiness: ReadinessReading
    let figure: MetricReading?
    /// The day the screen is about. When the reading is from an earlier day, the row says which.
    let shownDay: CalendarDay
    let today: CalendarDay

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .largeTitle) private var sparklineHeight: CGFloat = 52

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 10))
            : AnyLayout(HStackLayout(alignment: .center, spacing: 16))

        layout {
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text("\(readiness.score)")
                        .font(.largeTitle.weight(.semibold).monospacedDigit())
                    Text(readiness.band.label)
                        .font(.headline)
                        .foregroundStyle(readiness.band.colour)
                }
                if let comparison {
                    Text(comparison)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            // Its own width beside the sparkline, so the comparison never wraps a word at a time;
            // stacked at the accessibility sizes, it wraps like any text.
            .fixedSize(horizontal: !dynamicTypeSize.isAccessibilitySize, vertical: true)
            .layoutPriority(1)

            if let figure, figure.recent.count >= 2 {
                Sparkline(points: figure.recent, baseline: figure.baseline?.mean, mark: readiness.band.markColour)
                    .frame(maxWidth: .infinity)
                    .frame(height: sparklineHeight)
            }
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(title)
        .accessibilityValue("\(readiness.score), \(readiness.band.label)\(comparison.map { ", \($0)" } ?? "")")
    }

    private var title: String {
        readiness.day == shownDay
            ? "Readiness"
            : "Readiness · \(Formatting.day(RelativeDay.of(readiness.day, today: today)))"
    }

    private var comparison: String? {
        guard let figure, let deviation = figure.deviation else { return nil }
        return "\(Formatting.deviation(deviation, of: .readiness)) vs baseline"
    }
}

private extension ReadinessReading.Band {
    /// Colour only for the band that asks for attention, in the orange every figure row uses for a
    /// day outside the usual range — one signal colour across the screen rather than a traffic
    /// light, whose yellow does not read as text on a light background anyway.
    var colour: Color {
        switch self {
        case .payAttention: .orange
        case .good, .optimal: Color(.secondaryLabel)
        }
    }

    var markColour: Color {
        switch self {
        case .payAttention: .orange
        case .good, .optimal: Color(.label)
        }
    }
}
