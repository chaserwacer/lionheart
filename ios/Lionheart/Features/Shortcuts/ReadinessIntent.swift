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
        if !(file.read()?.readiness.map { TodayGlance.isToday($0.day, at: now) } ?? false) {
            let container = container
            await AnswerWait.atMost(Self.patience) { await container.readTodayForShortcut() }
        }

        guard let readiness = file.read()?.readiness else {
            return .result(dialog: "Lionheart has no readiness from Oura yet.")
        }
        let lead = TodayGlance.isToday(readiness.day, at: now)
            ? "Readiness"
            : "\(TodayGlance.dayName(readiness.day, at: now))'s readiness"
        let comparison = readiness.comparison.map { ", \($0)" } ?? ""
        return .result(
            dialog: "\(lead) \(readiness.score), \(readiness.band.lowercased())\(comparison).",
            view: ReadinessGlanceView(readiness: readiness, now: now)
                .frame(height: 120)
                .padding())
    }
}

/// Waits for a read for at most a spoken question's patience, and then answers either way.
///
/// Not `Deadline`, which cancels the late work and then waits for it to finish: a store's read runs
/// in a task of its own that a cancelled caller does not stop, so a deadline over it waits as long
/// as the read does. Here the read is left running — its answer still reaches the screen and the
/// widgets — and only the waiting stops.
@MainActor
private enum AnswerWait {
    static func atMost(_ limit: Duration, for read: @escaping @MainActor () async -> Void) async {
        await withCheckedContinuation { (answered: CheckedContinuation<Void, Never>) in
            let once = Once(answered)
            Task { await read(); once.resume() }
            Task { try? await Task.sleep(for: limit); once.resume() }
        }
    }

    /// Resumes its continuation the first time it is asked, and never again.
    private final class Once {
        private var continuation: CheckedContinuation<Void, Never>?

        init(_ continuation: CheckedContinuation<Void, Never>) {
            self.continuation = continuation
        }

        func resume() {
            continuation?.resume()
            continuation = nil
        }
    }
}
