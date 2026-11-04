import SwiftUI

/// The app's root: a sign-in form, or the five tabs.
///
/// Which one is `AuthStore.session`'s answer, and `.undetermined` is a case so the form does not
/// flash for a moment on every cold launch of an app that is already signed in.
///
/// The tint is applied **once**, here, and everything below inherits it. That is the whole of the
/// brand in this app — one token from the asset catalog — which is what keeps light and dark
/// correct without a single colour literal anywhere in a view.
struct RootView: View {
    let container: AppContainer

    @Environment(\.scenePhase) private var scenePhase

    var body: some View {
        Group {
            switch container.auth.session {
            case .undetermined:
                ProgressView().controlSize(.large)
            case .signedOut:
                SignInView(auth: container.auth)
            case .signedIn:
                // Signed in is not the same as set up. An Identity account with no Lionheart
                // profile is refused by every training write and answered with nothing by every
                // read, so the gate stands in front of the tabs rather than inside them.
                ProfileGateView(store: container.profile) {
                    SignedInTabs(container: container)
                }
            }
        }
        .tint(Tokens.accent)
        .environment(container.auth)
        // Read by the screens that have to say what is waiting on the server: Settings, before a
        // sign-out that would lose it, and Chat, which cannot work without it.
        .environment(container.outbox)
        .environment(container.reachability)
        // Read by History's day and week pages, which three stacks reach — History's, Today's and
        // the logging surface's — through `HistoryDestination`.
        .environment(container.diary)
        .environment(container.cardio)
        .environment(container.habits)
        .task { await container.start() }
        // Coming back to the app is when the athlete expects it to have caught up, so whatever was
        // waiting on the server is tried again then.
        .onChange(of: scenePhase) { _, phase in
            switch phase {
            case .active:
                Task { await container.enteredForeground() }
            case .background:
                container.reachability.leftForeground()
            case .inactive:
                break
            @unknown default:
                break
            }
        }
    }
}

/// The five tabs, chosen so each answers one question the athlete asks.
///
/// The glyphs are one set of outlined instrument marks rather than illustrations — a gauge for the
/// day's readiness, the record mark for logging, the history clock, a speech bubble, an ECG trace —
/// so no tab draws a figure or an object and the bar reads as a measuring tool's.
///
/// All five read live data. The selection is held here as `@State` rather than left to the
/// `TabView`, because the workout accessory is hung conditionally and would otherwise reset the
/// athlete's tab the moment a workout started — see `WorkoutAccessory`.
private struct SignedInTabs: View {
    let container: AppContainer

    /// Which tab is showing. Held here rather than left to `TabView`'s own storage because the
    /// workout bar taps *into* a screen, and that navigation has to be able to move the selection.
    @State private var selection: Destination = .today
    @State private var isWorkoutPresented = false
    /// The readout of a workout just finished, waiting for the logging surface to finish leaving:
    /// one sheet cannot be presented while the one it replaces is still being dismissed.
    @State private var finishedSummary: WorkoutSummary?
    @State private var shownSummary: WorkoutSummary?
    @Environment(\.scenePhase) private var scenePhase

    enum Destination: Hashable {
        case today, train, history, chat, body
    }

    var body: some View {
        TabView(selection: $selection) {
            Tab(value: Destination.today) {
                TodayView(
                    store: container.today,
                    ouraSync: container.ouraSync,
                    habits: container.habits,
                    library: container.movements,
                    history: container.history,
                    focus: container.focus,
                    apiBaseURL: container.configuration.baseURL,
                    activeWorkout: container.activeWorkout)
            } label: {
                TabLabel("Today", systemImage: "gauge.with.dots.needle.50percent")
            }
            Tab(value: Destination.train) {
                TrainView(
                    store: container.train,
                    activeWorkout: container.activeWorkout,
                    outbox: container.outbox,
                    library: container.movements,
                    routines: container.routines,
                    bodyweight: container.bodyweight,
                    openWorkout: { isWorkoutPresented = true })
            } label: {
                TabLabel("Train", systemImage: "record.circle")
            }
            Tab(value: Destination.history) {
                HistoryView(store: container.history, focus: container.focus, library: container.movements)
            } label: {
                TabLabel("History", systemImage: "clock.arrow.circlepath")
            }
            Tab(value: Destination.chat) {
                ThreadView(thread: container.thread, memory: container.chatMemory, chat: container.chat)
            } label: {
                TabLabel("Chat", systemImage: "bubble.left")
            }
            Tab(value: Destination.body) {
                BodyView(
                    store: container.body,
                    ouraSync: container.ouraSync,
                    cardio: container.cardio,
                    habits: container.habits,
                    wellness: container.wellness)
            } label: {
                TabLabel("Body", systemImage: "waveform.path.ecg")
            }
        }
        .minimizingTabBarOnScroll()
        // Coming back to the app is when the ring's night has usually reached Oura, so a store
        // that is behind syncs then — at most once per `OuraSyncPlan.automaticRetryInterval`.
        .onChange(of: scenePhase, initial: true) { _, phase in
            switch phase {
            case .active: Task { await container.ouraSync.syncIfBehind() }
            case .inactive, .background: break
            @unknown default: break
            }
        }
        .workoutAccessory(
            container.activeWorkout.workout,
            isPersisted: container.activeWorkout.isPersisted,
            // Resolved here because the bar is in `Design/` and does not reach for a store. The
            // library is already loaded for the Train tab, so this costs nothing.
            movementTitle: container.movements.movement(
                container.activeWorkout.workout?.currentMovementDataId ?? UUID())?.title) {
            isWorkoutPresented = true
        }
        // A sheet, not a full-screen cover, and measured rather than chosen for looks. Inside a
        // `fullScreenCover`, iOS 26 gave the entry row's text fields no keyboard toolbar until
        // something had been presented from within the cover — the field's `inputAccessoryView`
        // was nil — so the number pad came up without Done and Log on every surface opened from the
        // bar, from Continue or after a launch, and the row's own Log sat under the keys. As a sheet
        // the toolbar is there on the first focus. Swiping it down is the now-playing gesture, and
        // loses nothing Close would not.
        .sheet(isPresented: $isWorkoutPresented, onDismiss: {
            shownSummary = finishedSummary
            finishedSummary = nil
        }) {
            ActiveWorkoutView(
                store: container.activeWorkout,
                library: container.movements,
                bodyweight: container.bodyweight,
                routines: container.routines,
                finished: { finishedSummary = $0 },
                history: container.history,
                focus: container.focus)
        }
        .sheet(item: $shownSummary) { summary in
            WorkoutSummaryView(summary: summary, library: container.movements, routines: container.routines)
        }
    }
}

/// A tab's label, drawn in the symbol's outline form.
///
/// The tab bar swaps in a symbol's `.fill` variant wherever one exists, which would draw two of the
/// five glyphs solid and three as outlines. One weight across the set is the point of the set, so
/// the variant is pinned here rather than left to which symbols happen to have a fill.
private struct TabLabel: View {
    let title: String
    let systemImage: String

    init(_ title: String, systemImage: String) {
        self.title = title
        self.systemImage = systemImage
    }

    var body: some View {
        Label(title, systemImage: systemImage)
            .environment(\.symbolVariants, .none)
    }
}
