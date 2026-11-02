import SwiftUI

/// Settings, behind a toolbar button on Today rather than in a sixth tab.
///
/// It holds what the foundation genuinely has: signing out, and which origin this build is talking
/// to — which is the first thing worth seeing when a device build is pointed somewhere unexpected.
struct SettingsSheet: View {
    let apiBaseURL: URL

    @Environment(AuthStore.self) private var auth
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
                Text("Anything logged is on the server. A workout in progress stays on this device.")
            }
        }
    }
}
