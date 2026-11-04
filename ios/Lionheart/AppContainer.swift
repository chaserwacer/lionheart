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
    let reachability: ServerReachability
    let profile: ProfileStore
    let today: TodayStore
    let history: HistoryStore
    let focus: FocusStore
    let balance: TrainingBalanceStore
    let chat: ChatStore
    let thread: ThreadStore
    let chatMemory: MemoryStore
    let body: BodyStore
    let ouraSync: OuraSyncStore
    let cardio: CardioStore
    let habits: HabitsStore
    let diary: DiaryStore
    let wellness: WellnessStore
    let activeWorkout: ActiveWorkoutStore
    let outbox: WorkoutOutbox
    let train: TrainStore
    let movements: MovementLibraryStore
    let routines: RoutineStore
    let bodyweight: BodyweightStore

    private let tokens: TokenProvider
    private let memory: ReadMemory
    private let users: UserAPI
    private var hasStarted = false

    init(configuration: APIConfiguration = APIConfiguration()) {
        let tokens = TokenProvider(configuration: configuration)
        let reachability = ServerReachability()
        let memory = ReadMemory()
        let client = APIClient(
            configuration: configuration, tokens: tokens, memory: memory, reachability: reachability)
        let sessions = TrainingSessionsAPI(client: client)

        self.configuration = configuration
        self.tokens = tokens
        self.reachability = reachability
        self.memory = memory
        users = UserAPI(client: client)
        auth = AuthStore(identity: IdentityAPI(client: client), tokens: tokens)
        profile = ProfileStore(users: users)
        today = TodayStore(
            sessions: sessions,
            oura: OuraAPI(client: client),
            interpretation: InterpretationAPI(client: client))
        // Built before the workout store, which names the current movement on the Lock Screen and
        // so needs to be able to ask this for a title. Nothing here reaches back the other way.
        let movements = MovementLibraryStore(
            movements: MovementsAPI(client: client), records: PersonalRecordsAPI(client: client),
            sessions: sessions)
        self.movements = movements
        let outbox = WorkoutOutbox(sessions: sessions)
        self.outbox = outbox
        // The haptic and the movement's name are handed in rather than reached for, so nothing in
        // `Store/ActiveWorkout/` imports UIKit or depends on another store.
        let activeWorkout = ActiveWorkoutStore(
            sessions: sessions,
            outbox: outbox,
            activity: WorkoutLiveActivity(),
            restAlarm: RestNotification(),
            onRestFinished: { Haptics.restFinished() },
            movementTitle: { movements.movement($0)?.title },
            onServerUnanswered: { reachability.record(answered: false) })
        self.activeWorkout = activeWorkout
        train = TrainStore(sessions: sessions, activeWorkout: activeWorkout, outbox: outbox)
        routines = RoutineStore(routines: RoutinesAPI(client: client))
        bodyweight = BodyweightStore(bodyweight: BodyweightAPI(client: client))
        history = HistoryStore(
            sessions: TrainingSessionsAPI(client: client),
            library: movements,
            records: PersonalRecordsAPI(client: client))
        focus = FocusStore(api: FocusAPI(client: client))
        balance = TrainingBalanceStore(balance: TrainingBalanceAPI(client: client), focus: FocusAPI(client: client))
        chat = ChatStore(
            chat: ChatAPI(client: client),
            profile: ProfileAPI(client: client))
        thread = ThreadStore(api: ChatThreadAPI(client: client))
        chatMemory = MemoryStore(api: ChatMemoryAPI(client: client))
        body = BodyStore(
            oura: OuraAPI(client: client),
            wellness: WellnessAPI(client: client),
            injuries: InjuriesAPI(client: client))
        ouraSync = OuraSyncStore(oura: OuraAPI(client: client))
        cardio = CardioStore(cardio: CardioAPI(client: client), oura: OuraAPI(client: client))
        habits = HabitsStore(habits: HabitsAPI(client: client))
        diary = DiaryStore(
            oura: OuraAPI(client: client), cardio: CardioAPI(client: client), wellness: WellnessAPI(client: client))
        wellness = WellnessStore(wellness: WellnessAPI(client: client), oura: OuraAPI(client: client))
    }

    /// Settles the signed-in root and reads back anything that survived the last run.
    ///
    /// Guarded because `RootView.task` runs again whenever that view is recreated, and registering
    /// the sign-out teardown a second time would run it twice.
    func start() async {
        guard !hasStarted else { return }
        hasStarted = true

        // A sync stores new days, so the two tabs that show them read again — Body only when it has
        // been opened, since an unopened tab reads for itself when it is.
        ouraSync.onSynced { [today, body, cardio, habits, diary, balance] in
            await today.refresh()
            if body.state.value != nil { await body.refresh() }
            await cardio.refreshLoaded()
            await habits.refreshLoaded()
            // A sync re-pulls from the newest stored day, so only the last week's pages can change.
            await diary.refreshLoaded(since: CalendarDay(Date()).adding(days: -7))
            // New heart rate is new cardio, and new readiness can change what is up next.
            if balance.score.value != nil { await balance.refresh() }
        }
        // Today holds its own window of sessions, so a correction made from History shows there too.
        history.onRevised { [today, balance] in
            await today.refresh()
            // A corrected session changes the week's hard sets and its score.
            await balance.refresh()
        }
        // A rating shows on Body's row for it and on the day it rates.
        wellness.onLogged { [body, diary] entry in
            await body.refresh()
            await diary.refresh(around: entry.day)
        }

        // Registered before the session is settled, so a sign-out during the first load still
        // empties the screen it was loading into. The profile gate is reset for a sharper reason
        // than the screens are: left settled, the next athlete on this device would walk straight
        // into the tabs on an account that has no profile behind them.
        auth.onSignOut {
            [profile, today, train, routines, bodyweight, movements, history, focus, balance, chat, thread,
             chatMemory, body, ouraSync, cardio, habits, diary, wellness, memory] in
            profile.reset()
            today.reset()
            train.reset()
            routines.reset()
            bodyweight.reset()
            movements.reset()
            history.reset()
            focus.reset()
            balance.reset()
            chat.reset()
            thread.reset()
            chatMemory.reset()
            body.reset()
            ouraSync.reset()
            cardio.reset()
            habits.reset()
            diary.reset()
            wellness.reset()
            // Every answer the screens remembered. The next athlete on this phone must never open
            // onto the last one's readiness, offline or not.
            memory.forget()
        }
        // The workout goes too, document and all, and the finished ones waiting to be delivered:
        // they belong to the account, and ones the next sign-in could adopt would land one
        // athlete's training in another's log. Not on a sign-out the server forces, which keeps
        // them for the same athlete signing back in (`AccountCustody`).
        auth.onAccountChange { [activeWorkout, outbox] in
            activeWorkout.reset()
            outbox.reset()
        }
        // A workout that waited in the outbox is now the last session on its movements and of its
        // routine, so what the next workout opens on is read again once it lands.
        outbox.onLanded { [movements, routines, balance] in
            await movements.refresh()
            await routines.refresh()
            await balance.refresh()
        }
        auth.onSignIn { [weak self] in
            Task { await self?.signedIn() }
        }

        // The cheapest authenticated read there is, asked only while the server is away. The
        // remembered one, for its short patience: a probe that waits the system's minute notices
        // the server's return a minute late.
        let users = users
        reachability.probe { _ = try? await users.profileBoot(remembering: .profileBoot) }
        reachability.onReturn { [weak self] in
            Task { await self?.serverReturned() }
        }

        async let restored: Void = auth.restore()
        async let workout: Void = activeWorkout.restore()
        async let finished: Void = outbox.restore()
        _ = await (restored, workout, finished)

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
            await outbox.deliver()
        }
    }

    /// The app came to the foreground. Whatever is waiting on the server is tried again: an
    /// unreachable one is asked straight away, and a reachable one is handed what was left over.
    func enteredForeground() async {
        reachability.enteredForeground()
        guard case .signedIn = auth.session, reachability.isReachable else { return }
        await activeWorkout.serverReturned()
        if outbox.hasDeliverableWork { await outbox.deliver() }
    }

    /// An athlete has just signed in.
    ///
    /// The movement catalogue is loaded first and unconditionally. Sign-out leaves it `.loading`, which
    /// `serverReturned` does not count as waiting on the server, so without this a sign-in left every
    /// movement on Today reading "Unknown movement" until the Train tab happened to load it.
    private func signedIn() async {
        await movements.refresh()
        await serverReturned()
    }

    /// The server answered again after a spell of not answering, or an athlete has just signed in.
    ///
    /// The workout in flight is synced first (a deferred start opens its session here), then the
    /// finished workouts are delivered, then every screen still showing what it last knew, or
    /// nothing for want of the server, is read again — in that order, so the screens read a server
    /// that already holds what was delivered.
    private func serverReturned() async {
        guard case .signedIn = auth.session else { return }

        await activeWorkout.serverReturned()
        await outbox.deliver()

        if case .unreachable = profile.gate { await profile.refresh() }
        await withDiscardingTaskGroup { group in
            if today.state.isWaitingOnServer { group.addTask { await self.today.refresh() } }
            if train.state.isWaitingOnServer { group.addTask { await self.train.refresh() } }
            if routines.state.isWaitingOnServer { group.addTask { await self.routines.refresh() } }
            if bodyweight.state.isWaitingOnServer { group.addTask { await self.bodyweight.refresh() } }
            if history.state.isWaitingOnServer { group.addTask { await self.history.refresh() } }
            if focus.state.isWaitingOnServer { group.addTask { await self.focus.refresh() } }
            if balance.isWaitingOnServer { group.addTask { await self.balance.refresh() } }
            if body.state.isWaitingOnServer { group.addTask { await self.body.refresh() } }
            if cardio.isWaitingOnServer { group.addTask { await self.cardio.refreshLoaded() } }
            if habits.isWaitingOnServer { group.addTask { await self.habits.refreshLoaded() } }
            if movements.state.isWaitingOnServer { group.addTask { await self.movements.refresh() } }
            if case .unavailable = chat.state { group.addTask { await self.chat.refresh() } }
            if thread.transcript.isWaitingOnServer { group.addTask { await self.thread.refresh() } }
        }
    }
}
