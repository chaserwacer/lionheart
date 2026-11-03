import SwiftUI

/// The two `List` rows every `LoadState`-backed screen needs, which stock SwiftUI has no answer
/// for: a first load in progress, and a loaded screen that could not be refreshed.
///
/// `ContentUnavailableView` covers the empty and failed cases, and `ProgressView` covers the
/// spinner — but the *rows* around them are this app's, and History, Coach and Body all draw them
/// identically. Three copies of the stale banner would be three chances for one of them to start
/// blanking its screen instead.

/// What a screen shows while its first load is out.
struct LoadingRow: View {
    var body: some View {
        Section {
            HStack(spacing: 10) {
                ProgressView()
                Text("Loading").foregroundStyle(Color(.secondaryLabel))
            }
        }
    }
}

/// The note that a loaded screen is not current.
///
/// The value stays on screen and this says it could not be updated — the other half of
/// `LoadState.stale`, whose whole purpose is that a failed refresh in a basement does not blank
/// last week's readiness.
struct StaleBanner: View {
    let failure: LoadFailure

    var body: some View {
        Section {
            Label(failure.message, systemImage: "arrow.triangle.2.circlepath.exclamationmark")
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }
}
