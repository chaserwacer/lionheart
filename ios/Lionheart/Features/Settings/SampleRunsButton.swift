#if DEBUG && targetEnvironment(simulator)
import SwiftUI

/// Debug simulator builds only: writes the invented sample runs to this device's Health store, then syncs, so
/// the import and the run page can be seen in a simulator.
struct SampleRunsButton: View {
    let healthSync: HealthSyncStore

    @State private var outcome: String?

    var body: some View {
        Button("Write sample runs to Health") {
            Task {
                do {
                    try await SampleRuns.write(to: HealthLibrary().store)
                    outcome = "Written. Syncing."
                    await healthSync.refreshStatus()
                    await healthSync.sync()
                    outcome = nil
                } catch {
                    outcome = error.localizedDescription
                }
            }
        }
        if let outcome {
            Text(outcome).font(.footnote).foregroundStyle(Color(.secondaryLabel))
        }
    }
}
#endif
