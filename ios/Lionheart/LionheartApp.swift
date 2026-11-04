import AppIntents
import SwiftUI

@main
struct LionheartApp: App {
    /// The container is `@State` because this view creates it, which is the Observation rule: the
    /// view that makes an observable object holds it in `@State` and everything below reads it.
    @State private var container: AppContainer

    init() {
        let container = AppContainer()
        _container = State(initialValue: container)
        // The Shortcuts and Siri intents run in this process and act through the same stores the
        // screens do, so what they log or read is what the screens show.
        AppDependencyManager.shared.add(dependency: container)
    }

    var body: some Scene {
        let container = container
        WindowGroup {
            RootView(container: container)
        }
        .backgroundTask(.appRefresh(BackgroundRefresh.identifier)) {
            await container.refreshInBackground()
        }
    }
}
