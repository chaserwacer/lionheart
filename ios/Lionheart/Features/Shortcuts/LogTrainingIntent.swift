import AppIntents

/// "Log training" — the plain-words log, from Siri, Shortcuts or the Action button: "bench 3 by 5
/// at 100 kilos".
///
/// The same request Today's plain-words sheet sends, through the same store, so what it logs shows
/// on Today and in History the moment the app is opened. Its outcomes are the sheet's, spoken.
struct LogTrainingIntent: AppIntent {
    static let title: LocalizedStringResource = "Log Training"
    static let description = IntentDescription(
        "Logs training in plain words, the way Today's plain-words log does.")

    @Parameter(title: "What you did", requestValueDialog: "What did you do?")
    var words: String

    static var parameterSummary: some ParameterSummary {
        Summary("Log \(\.$words)")
    }

    @Dependency private var container: AppContainer

    @MainActor
    func perform() async throws -> some IntentResult & ProvidesDialog {
        await container.start()
        guard case .signedIn = container.auth.session else {
            return .result(dialog: "Sign in to Lionheart first.")
        }

        let today = container.today
        await today.submitQuickLog(words)
        defer { today.dismissQuickLogOutcome() }

        switch today.quickLog {
        case .committed(let sessions, let contexts, let warnings):
            let said = ([Formatting.logged(sessions: sessions, contexts: contexts)] + warnings)
            return .result(dialog: "\(said.joined(separator: " "))")
        case .needsConfirmation:
            return .result(dialog: "Lionheart needs to know which movement you meant. Nothing was saved.")
        case .refused(let failure):
            return .result(dialog: "\(failure.message)")
        case .idle, .interpreting:
            return .result(dialog: "Nothing was logged.")
        }
    }
}
