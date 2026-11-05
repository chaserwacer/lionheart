import AppIntents
import SwiftUI
import UIKit

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
    /// Read when the sheet opens: the athlete changes it in the Settings app, not here.
    @State private var backgroundRefresh = BackgroundRefreshStanding.of(UIApplication.shared.backgroundRefreshStatus)

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
                    Text("Tempo reads the runs, rides, hikes and walks other apps write to Health — Strava's, a watch's — with their routes, heart rate, power and running form, and never writes there. Oura's and Strong's workouts are left out, since Tempo reads those directly; rides and hikes set records, walks and indoor workouts do not. Syncs when the app opens and when History or Cardio is pulled to refresh.")
                }

                Section {
                    LabeledContent("Morning update") {
                        Text(backgroundRefresh.label)
                            .foregroundStyle(backgroundRefresh.isOn ? Color(.secondaryLabel) : .orange)
                    }
                } header: {
                    Text("Widgets and Siri")
                } footer: {
                    Text("The Readiness and Today widgets show what Tempo last read. It reads today once each morning, around 5:30, so the Lock Screen is current before the app is opened — unless Background App Refresh is off for Tempo in the Settings app. Siri knows \u{201C}What\u{2019}s my readiness in Tempo\u{201D} and \u{201C}Log training in Tempo\u{201D}.")
                }

                Section {
                    ShortcutsLink()
                        .shortcutsLinkStyle(.automaticOutline)
                        .frame(maxWidth: .infinity)
                }
                .listRowBackground(Color.clear)

                if !auth.isSessionPersisted {
                    Section {
                        Label(
                            "This device could not save your sign-in, so Tempo will ask again next time it starts.",
                            systemImage: "exclamationmark.triangle.fill")
                            .foregroundStyle(.orange)
                    }
                }

                Section {
                    Button("Sign out", role: .destructive) { isConfirmingSignOut = true }
                }

                Section {
                    AboutRow()
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
                "Sign out of Tempo?",
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

/// The app naming itself: the mark, the wordmark, the maker's line and the build.
private struct AboutRow: View {
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 8))
            : AnyLayout(HStackLayout(alignment: .center, spacing: 14))
        layout {
            BrandMark()
                .frame(height: 30)
            VStack(alignment: .leading, spacing: 2) {
                Wordmark(size: 22)
                Text("Tempo Training Systems")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
            Text(version)
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 6)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Tempo Training Systems, version \(version)")
    }

    /// The marketing version and the build, as `project.yml` sets them.
    private var version: String {
        let info = Bundle.main.infoDictionary
        let marketing = info?["CFBundleShortVersionString"] as? String ?? "–"
        let build = info?["CFBundleVersion"] as? String ?? "–"
        return "\(marketing) (\(build))"
    }
}

/// Whether iOS will run the morning update, in the words Settings uses.
private enum BackgroundRefreshStanding {
    case on
    case off
    /// Turned off by something the athlete may not control here — Low Power Mode, a profile.
    case restricted

    static func of(_ status: UIBackgroundRefreshStatus) -> BackgroundRefreshStanding {
        switch status {
        case .available: .on
        case .denied: .off
        case .restricted: .restricted
        @unknown default: .off
        }
    }

    var isOn: Bool {
        switch self {
        case .on: true
        case .off, .restricted: false
        }
    }

    var label: String {
        switch self {
        case .on: "On"
        case .off: "Off"
        case .restricted: "Restricted"
        }
    }
}
