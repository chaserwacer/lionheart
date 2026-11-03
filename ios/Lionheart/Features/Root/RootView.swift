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
        .task { await container.start() }
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

    enum Destination: Hashable {
        case today, train, history, chat, body
    }

    var body: some View {
        TabView(selection: $selection) {
            Tab(value: Destination.today) {
                TodayView(store: container.today, apiBaseURL: container.configuration.baseURL)
            } label: {
                TabLabel("Today", systemImage: "gauge.with.dots.needle.50percent")
            }
            Tab(value: Destination.train) {
                TrainView(
                    store: container.train,
                    activeWorkout: container.activeWorkout,
                    library: container.movements,
                    openWorkout: { isWorkoutPresented = true })
            } label: {
                TabLabel("Train", systemImage: "record.circle")
            }
            Tab(value: Destination.history) {
                HistoryView(store: container.history)
            } label: {
                TabLabel("History", systemImage: "clock.arrow.circlepath")
            }
            Tab(value: Destination.chat) {
                ChatView(store: container.chat)
            } label: {
                TabLabel("Chat", systemImage: "bubble.left")
            }
            Tab(value: Destination.body) {
                BodyView(store: container.body)
            } label: {
                TabLabel("Body", systemImage: "waveform.path.ecg")
            }
        }
        .minimizingTabBarOnScroll()
        .workoutAccessory(
            container.activeWorkout.workout,
            isPersisted: container.activeWorkout.isPersisted,
            // Resolved here because the bar is in `Design/` and does not reach for a store. The
            // library is already loaded for the Train tab, so this costs nothing.
            movementTitle: container.movements.movement(
                container.activeWorkout.workout?.currentMovementDataId ?? UUID())?.title) {
            isWorkoutPresented = true
        }
        .fullScreenCover(isPresented: $isWorkoutPresented) {
            ActiveWorkoutView(
                store: container.activeWorkout,
                library: container.movements)
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
