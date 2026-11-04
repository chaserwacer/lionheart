import SwiftUI
import WidgetKit

/// Today in a glance on the Home Screen: the readiness that decides the day beside the training
/// either side of it — the last session done and the next one planned.
struct TodayWidget: Widget {
    static let kind = "com.chasebalmer.lionheart.today"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: Self.kind, provider: GlanceTimeline()) { entry in
            TodayWidgetView(entry: entry)
                .containerBackground(Color(.secondarySystemGroupedBackground), for: .widget)
        }
        .configurationDisplayName("Today")
        .description("Readiness, your last session and the next one planned.")
        .supportedFamilies([.systemMedium])
    }
}

private struct TodayWidgetView: View {
    let entry: GlanceEntry

    var body: some View {
        if let glance = entry.glance {
            HStack(alignment: .top, spacing: 16) {
                if let readiness = glance.readiness {
                    ReadinessGlanceView(readiness: readiness, now: entry.date)
                } else {
                    NoGlanceView(family: .systemSmall)
                }
                Divider()
                VStack(alignment: .leading, spacing: 10) {
                    SessionLine(caption: "Last", session: glance.lastSession, now: entry.date)
                    if let next = glance.nextSession {
                        SessionLine(caption: "Next", session: next, now: entry.date)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        } else {
            NoGlanceView(family: .systemMedium)
        }
    }
}

/// One session as the widget shows it: when, what, and what it came to.
private struct SessionLine: View {
    let caption: String
    let session: TodayGlance.Session?
    let now: Date

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            if let session {
                Text("\(caption) · \(TodayGlance.dayName(session.day, at: now))")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
                Text(session.headline)
                    .font(.subheadline.weight(.medium))
                    .lineLimit(2)
                if let facts = session.facts {
                    Text(facts)
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            } else {
                Text(caption)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)
                Text("Nothing logged")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
        }
        .accessibilityElement(children: .combine)
    }
}
