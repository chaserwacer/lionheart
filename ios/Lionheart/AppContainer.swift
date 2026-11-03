import Foundation
import Observation

/// What the app is made of, wired once.
///
/// Construction order is the dependency order and it is one way: configuration, then the token
/// provider that owns renewal, then the client, then the endpoint types, then the stores. Nothing
/// reaches back — which is what stops the client needing the token store *and* the token store
/// needing the client.
///
/// It is `@Observable` only so `RootView` can read `auth.session` through it; nothing here changes
/// after `start()`.
@Observable
@MainActor
final class AppContainer {
    let configuration: APIConfiguration
    let auth: AuthStore
    let today: TodayStore
    let activeWorkout: ActiveWorkoutStore

    private let tokens: TokenProvider
    private var hasStarted = false

    init(configuration: APIConfiguration = APIConfiguration()) {
        let tokens = TokenProvider(configuration: configuration)
        let client = APIClient(configuration: configuration, tokens: tokens)

        self.configuration = configuration
        self.tokens = tokens
        auth = AuthStore(identity: IdentityAPI(client: client), tokens: tokens)
        today = TodayStore(
            sessions: TrainingSessionsAPI(client: client),
            oura: OuraAPI(client: client),
            interpretation: InterpretationAPI(client: client))
        activeWorkout = ActiveWorkoutStore()
    }

    /// Settles the signed-in root and reads back anything that survived the last run.
    ///
    /// Guarded because `RootView.task` runs again whenever that view is recreated, and registering
    /// the sign-out teardown a second time would run it twice.
    func start() async {
        guard !hasStarted else { return }
        hasStarted = true

        // Registered before the session is settled, so a sign-out during the first load still
        // empties the screen it was loading into.
        auth.onSignOut { [today] in today.reset() }

        async let restored: Void = auth.restore()
        async let workout: Void = activeWorkout.restore()
        _ = await (restored, workout)
    }
}
