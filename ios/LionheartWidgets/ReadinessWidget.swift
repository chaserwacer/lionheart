import SwiftUI
import WidgetKit

/// The morning's readiness, on the Home Screen and the Lock Screen.
///
/// The Lock Screen is where it earns its place: the verdict read off the phone on the way out of
/// bed, before any app is opened. The circular family is Oura's score as a gauge out of 100, the
/// rectangular one adds the band and the baseline, the inline one is a line above the clock, and
/// the small Home Screen one draws the last weeks too.
struct ReadinessWidget: Widget {
    static let kind = "com.chasebalmer.lionheart.readiness"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: Self.kind, provider: GlanceTimeline()) { entry in
            ReadinessWidgetView(entry: entry)
        }
        .configurationDisplayName("Readiness")
        .description("Oura's readiness against your baseline.")
        .supportedFamilies([.systemSmall, .accessoryCircular, .accessoryRectangular, .accessoryInline])
    }
}

private struct ReadinessWidgetView: View {
    let entry: GlanceEntry

    @Environment(\.widgetFamily) private var family

    var body: some View {
        content
            .containerBackground(for: .widget) {
                if family == .systemSmall { Color(.secondarySystemGroupedBackground) }
            }
    }

    @ViewBuilder
    private var content: some View {
        if let readiness = entry.glance?.readiness {
            switch family {
            case .accessoryCircular:
                Gauge(value: Double(readiness.score), in: 0...100) {
                    Text("Ready")
                } currentValueLabel: {
                    Text("\(readiness.score)").monospacedDigit()
                }
                .gaugeStyle(.accessoryCircular)
                .widgetAccentable()
                .accessibilityLabel(title(readiness))
                .accessibilityValue("\(readiness.score), \(readiness.band)")
            case .accessoryRectangular:
                VStack(alignment: .leading, spacing: 0) {
                    Text(title(readiness))
                        .font(.headline)
                        .widgetAccentable()
                    Text("\(readiness.score) · \(readiness.band)")
                        .font(.body.monospacedDigit())
                    if let comparison = readiness.comparison {
                        Text(comparison)
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(.secondary)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityElement(children: .combine)
            case .accessoryInline:
                Text("Readiness \(readiness.score) · \(readiness.band)")
            default:
                ReadinessGlanceView(readiness: readiness, now: entry.date, showsMark: true)
            }
        } else {
            NoGlanceView(family: family)
        }
    }

    private func title(_ readiness: TodayGlance.Readiness) -> String {
        TodayGlance.isToday(readiness.day, at: entry.date)
            ? "Readiness"
            : TodayGlance.dayName(readiness.day, at: entry.date)
    }
}

/// What a widget says before the app has read a day on this phone, or after a sign-out.
struct NoGlanceView: View {
    let family: WidgetFamily

    var body: some View {
        switch family {
        case .accessoryCircular:
            Gauge(value: 0, in: 0...100) { Text("Ready") } currentValueLabel: { Text("–") }
                .gaugeStyle(.accessoryCircular)
        case .accessoryInline:
            Text("Readiness –")
        default:
            VStack(alignment: .leading, spacing: 4) {
                Text("Readiness")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
                Text("Open Tempo to read today.")
                    .font(.footnote)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
    }
}
