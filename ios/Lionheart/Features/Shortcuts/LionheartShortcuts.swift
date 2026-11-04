import AppIntents

/// The phrases Siri and Spotlight know Lionheart by, with no setup in the Shortcuts app.
///
/// Two, for the two things worth doing without opening the app: hearing the morning's verdict, and
/// logging what was just done while the hands are busy.
struct LionheartShortcuts: AppShortcutsProvider {
    static var appShortcuts: [AppShortcut] {
        AppShortcut(
            intent: ReadinessIntent(),
            phrases: [
                "What's my readiness in \(.applicationName)",
                "\(.applicationName) readiness",
            ],
            shortTitle: "Readiness",
            systemImageName: "gauge.with.dots.needle.50percent")
        AppShortcut(
            intent: LogTrainingIntent(),
            phrases: [
                "Log training in \(.applicationName)",
                "Log in \(.applicationName)",
            ],
            shortTitle: "Log Training",
            systemImageName: "square.and.pencil")
    }
}
