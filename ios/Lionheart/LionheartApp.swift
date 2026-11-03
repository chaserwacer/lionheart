import SwiftUI

@main
struct LionheartApp: App {
    /// The container is `@State` because this view creates it, which is the Observation rule: the
    /// view that makes an observable object holds it in `@State` and everything below reads it.
    @State private var container = AppContainer()

    var body: some Scene {
        WindowGroup {
            RootView(container: container)
        }
    }
}
