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
            Tab("Today", systemImage: "sun.horizon", value: Destination.today) {
                TodayView(store: container.today, apiBaseURL: container.configuration.baseURL)
            }
            Tab("Train", systemImage: "figure.strengthtraining.traditional", value: Destination.train) {
                TrainView(
                    store: container.train,
                    activeWorkout: container.activeWorkout,
                    library: container.movements,
                    openWorkout: { isWorkoutPresented = true })
            }
            Tab("History", systemImage: "list.bullet.rectangle", value: Destination.history) {
                HistoryView(store: container.history)
            }
            Tab("Chat", systemImage: "bubble.left.and.text.bubble.right", value: Destination.chat) {
                ChatView(store: container.chat)
            }
            Tab("Body", systemImage: "heart.text.square", value: Destination.body) {
                BodyView(store: container.body)
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
