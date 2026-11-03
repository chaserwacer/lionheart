import SwiftUI

/// A planned session, and the button that starts it.
///
/// It exists so that starting a session is not a blind tap: the athlete sees the targets first,
/// which is also what tells them whether this is the session they meant. The same targets reappear
/// on the logging surface, read from the workout's own document — they are carried into it rather
/// than re-fetched, because the gym is where the signal is worst.
struct PlannedSessionView: View {
    let session: TrainingSession.Planned?
    let store: TrainStore
    let library: MovementLibraryStore
    let openWorkout: () -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var isSkipAsked = false

    var body: some View {
        Group {
            if let session {
                content(session)
            } else {
                // The session left the loaded window — most likely it was just started or skipped,
                // so this screen is on its way out.
                ContentUnavailableView {
                    Label("This session is no longer planned", systemImage: "calendar.badge.exclamationmark")
                } description: {
                    Text("It may have been started, skipped or deleted somewhere else.")
                }
            }
        }
        .navigationTitle("Planned session")
        .navigationBarTitleDisplayMode(.inline)
    }

    private func content(_ session: TrainingSession.Planned) -> some View {
        List {
            Section {
                LabeledContent("Planned for") {
                    Text(Formatting.date(session.plannedFor))
                }
                if !session.envelope.notes.isEmpty {
                    Text(session.envelope.notes)
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }

            if session.prescriptions.isEmpty {
                Section {
                    Text("No targets on this session. Starting it gives you an empty workout to log into.")
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } else {
                targetSections(session)
            }

            Section {
                Button {
                    Task {
                        if await store.start(planned: session) {
                            openWorkout()
                            dismiss()
                        }
                    }
                } label: {
                    Label("Start this session", systemImage: "play.circle")
                        .fontWeight(.semibold)
                }
                .disabled(store.isWorkoutInFlight || isStarting)

                Button(role: .destructive) {
                    isSkipAsked = true
                } label: {
                    Label("Mark as skipped", systemImage: "xmark.circle")
                }
            } footer: {
                if store.isWorkoutInFlight {
                    Text("Finish the workout you are logging before starting another.")
                } else {
                    Text("Starting it records the session on today, whatever day it was planned for.")
                }
            }
        }
        .listStyle(.insetGrouped)
        .confirmationDialog(
            "Mark this session as skipped?",
            isPresented: $isSkipAsked,
            titleVisibility: .visible
        ) {
            Button(role: .destructive) {
                Task {
                    await store.skip(session)
                    dismiss()
                }
            } label: {
                Text("Mark as skipped")
            }
        } message: {
            Text("A skipped session is kept — the skip is data. It cannot be un-skipped.")
        }
    }

    /// The targets, grouped by movement in prescribed order, which is the order they will appear in
    /// on the logging surface.
    @ViewBuilder
    private func targetSections(_ session: TrainingSession.Planned) -> some View {
        let reading = WorkoutReading.of(sets: [], prescriptions: session.prescriptions)

        ForEach(reading.movements) { movement in
            Section(library.title(of: movement.movementDataId)) {
                ForEach(Array(movement.targets.enumerated()), id: \.offset) { index, target in
                    LabeledContent("Set \(index + 1)", value: Formatting.target(target))
                        .monospacedDigit()
                }
            }
        }

        if !reading.looseTargets.isEmpty {
            Section("Other") {
                ForEach(Array(reading.looseTargets.enumerated()), id: \.offset) { _, target in
                    Text(Formatting.target(target))
                }
            }
        }
    }

    private var isStarting: Bool {
        if case .starting = store.starting { return true }
        return false
    }
}
