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
/// Four of them are honest placeholders. Today is the one that reads live data, because this change
/// is the floor the training and analysis screens get built on rather than those screens.
private struct SignedInTabs: View {
    let container: AppContainer

    /// Which tab is showing. Held here rather than left to `TabView`'s own storage because the
    /// workout bar taps *into* a screen, and that navigation has to be able to move the selection.
    @State private var selection: Destination = .today
    @State private var isWorkoutPresented = false

    enum Destination: Hashable {
        case today, train, history, coach, body
    }

    var body: some View {
        TabView(selection: $selection) {
            Tab("Today", systemImage: "sun.horizon", value: Destination.today) {
                TodayView(store: container.today, apiBaseURL: container.configuration.baseURL)
            }
            Tab("Train", systemImage: "figure.strengthtraining.traditional", value: Destination.train) {
                TrainView()
            }
            Tab("History", systemImage: "list.bullet.rectangle", value: Destination.history) {
                HistoryView(store: container.history)
            }
            Tab("Coach", systemImage: "bubble.left.and.text.bubble.right", value: Destination.coach) {
                CoachView(store: container.coach)
            }
            Tab("Body", systemImage: "heart.text.square", value: Destination.body) {
                BodyView(store: container.body)
            }
        }
        .workoutAccessory(
            container.activeWorkout.workout,
            isPersisted: container.activeWorkout.isPersisted) {
            isWorkoutPresented = true
        }
        .fullScreenCover(isPresented: $isWorkoutPresented) {
            ActiveWorkoutPlaceholder()
        }
    }
}

/// Where the full-screen logging surface will live.
///
/// It exists so the bar's tap target goes somewhere real and the seam is complete: the
/// training-feature agent replaces this view, not the plumbing around it.
private struct ActiveWorkoutPlaceholder: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            ContentUnavailableView {
                Label("Logging a workout", systemImage: "figure.strengthtraining.traditional")
            } description: {
                Text("The set-by-set logging surface is the next thing being built. The workout bar, its persistence and its two OS paths are already in place.")
            }
            .navigationTitle("Workout")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Close") { dismiss() }
                }
            }
        }
    }
}
