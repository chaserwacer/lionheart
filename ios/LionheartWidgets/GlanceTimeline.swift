import WidgetKit

/// One moment's glance: what the app last left, drawn as of `date`.
struct GlanceEntry: TimelineEntry {
    let date: Date
    /// `nil` before the app has read today even once on this phone, or after a sign-out.
    let glance: TodayGlance?
}

/// The widgets' timeline: the glance the app left, now, and the same glance again at each of the
/// next week's midnights.
///
/// Nothing here reaches the server. The app writes the glance and asks WidgetKit to reload when it
/// changes (`TodayGlancePublisher`), so the timeline never expires on its own; its later entries
/// are where the glance's days have to be named again — "Today" becomes "Yesterday", then a
/// weekday, with no new data.
struct GlanceTimeline: TimelineProvider {
    func placeholder(in context: Context) -> GlanceEntry {
        GlanceEntry(date: Date(), glance: .placeholder)
    }

    func getSnapshot(in context: Context, completion: @escaping (GlanceEntry) -> Void) {
        let kept = TodayGlanceFile().read()
        completion(GlanceEntry(date: Date(), glance: context.isPreview ? kept ?? .placeholder : kept))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<GlanceEntry>) -> Void) {
        let now = Date()
        let glance = TodayGlanceFile().read()
        completion(Timeline(
            entries: [GlanceEntry(date: now, glance: glance)]
                + Self.midnights(after: now).map { GlanceEntry(date: $0, glance: glance) },
            policy: .never))
    }

    /// The next week's midnights, a minute past each, by the calendar rather than by adding a
    /// day's seconds — a night the clocks change on is not 86,400 seconds long. A week, so a glance
    /// left unchanged for days still names its days rightly; the app reloads the timeline long
    /// before the last one when it runs at all.
    static func midnights(after now: Date, calendar: Calendar = .current) -> [Date] {
        let today = calendar.startOfDay(for: now)
        return (1...7).compactMap { offset in
            calendar.date(byAdding: .day, value: offset, to: today)
                .flatMap { calendar.date(byAdding: .minute, value: 1, to: $0) }
        }
    }
}

extension TodayGlance {
    /// The widget gallery's stand-in, drawn redacted: shapes, never his numbers.
    static let placeholder = TodayGlance(
        readiness: Readiness(
            score: 80, band: "Good", asksForAttention: false, comparison: "+2 vs baseline",
            recent: [76, 79, 74, 81, 78, 83, 80], day: Date()),
        lastSession: Session(day: Date().addingTimeInterval(-86_400), headline: "Squat, Bench press", facts: "18 sets · 1 h"),
        nextSession: nil,
        asOf: Date())
}
