import SwiftUI

/// What the logging surface says when the sets have not reached the server: reassurance and a retry
/// when a push failed, the server's words when it refused them.
struct WorkoutSyncSection: View {
    let sync: WorkoutSyncState
    let retry: () -> Void

    var body: some View {
        Section {
            switch sync {
            case .synced, .pending:
                EmptyView()
            case .failed(let reason):
                VStack(alignment: .leading, spacing: 4) {
                    Label("Not synced yet", systemImage: "arrow.triangle.2.circlepath")
                        .font(.subheadline.weight(.semibold))
                    Text("Your sets are saved on this phone. \(reason)")
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                    Button("Try again", action: retry)
                        .font(.footnote)
                }
            case .refused(let reason):
                VStack(alignment: .leading, spacing: 4) {
                    Label("The server will not take these sets", systemImage: "exclamationmark.triangle.fill")
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(.orange)
                    Text(reason)
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
    }
}
