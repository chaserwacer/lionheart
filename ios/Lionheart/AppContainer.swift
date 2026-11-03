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
    let profile: ProfileStore
    let today: TodayStore
    let history: HistoryStore
    let coach: CoachStore
    let body: BodyStore
    let activeWorkout: ActiveWorkoutStore
    let train: TrainStore
    let movements: MovementLibraryStore

    private let tokens: TokenProvider
    private var hasStarted = false

    init(configuration: APIConfiguration = APIConfiguration()) {
        let tokens = TokenProvider(configuration: configuration)
        let client = APIClient(configuration: configuration, tokens: tokens)
        let sessions = TrainingSessionsAPI(client: client)

        self.configuration = configuration
        self.tokens = tokens
        auth = AuthStore(identity: IdentityAPI(client: client), tokens: tokens)
        profile = ProfileStore(users: UserAPI(client: client))
        today = TodayStore(
            sessions: sessions,
            oura: OuraAPI(client: client),
            interpretation: InterpretationAPI(client: client))
        // The haptic is handed in rather than reached for, so nothing in `Store/` imports UIKit.
        let activeWorkout = ActiveWorkoutStore(
            sessions: sessions,
            onRestFinished: { Haptics.restFinished() })
        self.activeWorkout = activeWorkout
        train = TrainStore(sessions: sessions, activeWorkout: activeWorkout)
        movements = MovementLibraryStore(movements: MovementsAPI(client: client))
        history = HistoryStore(
            sessions: TrainingSessionsAPI(client: client),
            movements: MovementsAPI(client: client),
            records: PersonalRecordsAPI(client: client))
        coach = CoachStore(
            chat: ChatAPI(client: client),
            profile: ProfileAPI(client: client))
        body = BodyStore(
            oura: OuraAPI(client: client),
            wellness: WellnessAPI(client: client),
            injuries: InjuriesAPI(client: client))
    }

    /// Settles the signed-in root and reads back anything that survived the last run.
    ///
    /// Guarded because `RootView.task` runs again whenever that view is recreated, and registering
    /// the sign-out teardown a second time would run it twice.
    func start() async {
        guard !hasStarted else { return }
        hasStarted = true

        // Registered before the session is settled, so a sign-out during the first load still
        // empties the screen it was loading into. The profile gate is reset for a sharper reason
        // than the screens are: left settled, the next athlete on this device would walk straight
        // into the tabs on an account that has no profile behind them.
        auth.onSignOut { [profile, today, train, movements, history, coach, body, activeWorkout] in
            profile.reset()
            today.reset()
            train.reset()
            movements.reset()
            history.reset()
            coach.reset()
            body.reset()
            // The workout goes too, document and all: it belongs to the session the signed-out
            // credential owned, and one the next sign-in could adopt would push one athlete's sets
            // at another athlete's session id.
            activeWorkout.reset()
        }

        async let restored: Void = auth.restore()
        async let workout: Void = activeWorkout.restore()
        _ = await (restored, workout)

        // The movement catalogue is loaded here rather than by the Train tab alone, because the
        // workout bar names the movement being worked on and the bar is on every tab. Left to the
        // tab, a cold launch into a restored workout showed "Workout in progress" until the athlete
        // happened to visit Train — which is exactly the launch an athlete makes mid-session.
        //
        // Only once the session is settled and signed in: the route needs a bearer token, and
        // asking for one before `restore()` has decided would spend a doomed request on every cold
        // launch of a signed-out app.
        if case .signedIn = auth.session {
            await movements.refreshIfNeeded()
        }
    }
}
