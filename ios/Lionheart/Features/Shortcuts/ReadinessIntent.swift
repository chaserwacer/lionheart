import AppIntents
import SwiftUI

/// "What's my readiness?" — Oura's readiness against his baseline, spoken and shown, without
/// opening the app.
///
/// Answered from the widgets' glance, which is today's whenever the app has read today. When the
/// glance is from an earlier day, today is read first, for as long as a question asked out loud can
/// wait; a server that is slower than that gets the earlier day's answer, named as that day's.
struct ReadinessIntent: AppIntent {
    static let title: LocalizedStringResource = "Readiness"
    static let description = IntentDescription(
        "Oura's readiness for today against your baseline, as Lionheart read it.")

    /// How long a spoken question waits on the server before it is answered from the device.
    private static let patience: Duration = .seconds(4)

    @Dependency private var container: AppContainer

    @MainActor
    func perform() async throws -> some IntentResult & ProvidesDialog & ShowsSnippetView {
        let file = TodayGlanceFile()
        let now = Date()
        if file.read()?.readiness.map({ !Calendar.current.isDate($0.day, inSameDayAs: now) }) ?? true {
            let container = container
            _ = try? await Deadline.waiting(
                atMost: Self.patience,
                expiring: { CancellationError() },
                for: { await container.readTodayForShortcut() })
        }

        guard let readiness = file.read()?.readiness else {
            return .result(dialog: "Lionheart has no readiness from Oura yet.")
        }
        let day = TodayGlance.dayName(readiness.day, at: now)
        let lead = day == "Today" ? "Readiness" : "\(day)'s readiness"
        let comparison = readiness.comparison.map { ", \($0)" } ?? ""
        return .result(
            dialog: "\(lead) \(readiness.score), \(readiness.band.lowercased())\(comparison).",
            view: ReadinessGlanceView(readiness: readiness, now: now)
                .frame(height: 120)
                .padding())
    }
}
