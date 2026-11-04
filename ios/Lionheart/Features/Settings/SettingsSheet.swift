import SwiftUI

/// Settings, behind a toolbar button on Today rather than in a sixth tab.
///
/// It holds what the foundation genuinely has: signing out, which origin this build is talking
/// to — which is the first thing worth seeing when a device build is pointed somewhere unexpected —
/// and the Apple Health connection, with what it reads.
struct SettingsSheet: View {
    let apiBaseURL: URL
    /// Read only to say what signing out would cost it.
    let activeWorkout: ActiveWorkoutStore
    let healthSync: HealthSyncStore

    @Environment(AuthStore.self) private var auth
    /// Read only to count the finished workouts a sign-out would lose with everything else.
    @Environment(WorkoutOutbox.self) private var outbox
    @Environment(\.dismiss) private var dismiss

    @State private var isConfirmingSignOut = false

    var body: some View {
        NavigationStack {
            Form {
                Section("Connection") {
                    LabeledContent("Server") {
                        Text(apiBaseURL.absoluteString)
                            .font(.footnote.monospaced())
                            .foregroundStyle(Color(.secondaryLabel))
                            .multilineTextAlignment(.trailing)
                    }
                }

                Section {
                    HealthSyncRow(store: healthSync)
                    #if DEBUG && targetEnvironment(simulator)
                    SampleRunsButton(healthSync: healthSync)
                    #endif
                } header: {
                    Text("Apple Health")
                } footer: {
                    Text("Lionheart reads the runs other apps write to Health — Strava's, with its route and heart rate — and never writes there. Oura's and Strong's workouts are left out, since Lionheart reads those directly. Syncs when the app opens and when History or Cardio is pulled to refresh.")
                }

                if !auth.isSessionPersisted {
                    Section {
                        Label(
                            "This device could not save your sign-in, so Lionheart will ask again next time it starts.",
                            systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.orange)
                    }
                }

                Section {
                    Button("Sign out", role: .destructive) { isConfirmingSignOut = true }
                }
            }
            .navigationTitle("Settings")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
            .confirmationDialog(
                "Sign out of Lionheart?",
                isPresented: $isConfirmingSignOut,
                titleVisibility: .visible
            ) {
                Button("Sign out", role: .destructive) {
                    Task {
                        await auth.signOut()
                        dismiss()
                    }
                }
            } message: {
                Text(SignOutConsequence.of(activeWorkout.workout).message(undelivered: outbox.finished.count))
            }
        }
    }
}
