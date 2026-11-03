import SwiftUI

/// The root gate: the five tabs, or the one screen that makes them work.
///
/// It wraps the signed-in root rather than sitting inside it, because an athlete with no profile
/// has no use for any of the five tabs — every training write is refused and every read is empty —
/// and burying the fix in Settings would mean the one screen that unblocks the app is the one
/// screen you have to already know about.
struct ProfileGateView<Content: View>: View {
    let store: ProfileStore
    @ViewBuilder let content: () -> Content

    var body: some View {
        Group {
            switch store.gate {
            case .undetermined:
                ProgressView().controlSize(.large)
            case .absent:
                OnboardingView(store: store)
            case .present:
                content()
            case .unreachable(let failure):
                unreachable(failure)
            }
        }
        .task { await store.refresh() }
    }

    /// Neither other branch is honest when the boot read failed: the tabs would load into
    /// failures, and the form would offer to create a profile that may already exist.
    private func unreachable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label(
                "Lionheart cannot reach the server",
                systemImage: "antenna.radiowaves.left.and.right.slash")
        } description: {
            Text(failure.message)
        } actions: {
            Button("Try again") { Task { await store.refresh() } }
                .buttonStyle(.borderedProminent)
        }
    }
}
