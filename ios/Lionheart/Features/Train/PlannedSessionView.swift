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
    @State private var asked: SessionTransition?

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
        .toolbar {
            if session != nil {
                ToolbarItem(placement: .topBarTrailing) {
                    // The same two transitions the row on the tab offers under a long press. A
                    // screen reached by a tap still needs them reachable without one more tap into
                    // the page's body, which is where every first-party app puts a `Menu`.
                    Menu("More", systemImage: "ellipsis.circle") {
                        ForEach(destructiveTransitions) { transition in
                            Button(role: .destructive) {
                                asked = transition
                            } label: {
                                Label(transition.label, systemImage: transition.systemImage)
                            }
                        }
                    }
                    .disabled(store.action.isRunning)
                }
            }
        }
        .confirmationDialog(
            asked?.confirmation?.question ?? "",
            isPresented: Binding(get: { asked != nil }, set: { if !$0 { asked = nil } }),
            titleVisibility: .visible,
            presenting: asked
        ) { transition in
            if let confirmation = transition.confirmation {
                Button(confirmation.proceed, role: .destructive) { carryOut(transition) }
            }
        } message: { transition in
            if let confirmation = transition.confirmation { Text(confirmation.warning) }
        }
    }

    /// Skip and delete, read from the lifecycle state rather than written out here, so this screen
    /// and the row on the tab cannot come to offer different things.
    private var destructiveTransitions: [SessionTransition] {
        guard let session else { return [] }
        let kind = SessionLog.kind(of: .planned(session), today: CalendarDay(Date()))
        return SessionTransition.offered(on: kind).filter(\.isDestructive)
    }

    /// Carries the transition out and leaves, because the session this screen is about will not be
    /// planned any more. A refusal keeps the screen: the alert that states it is on the tab behind.
    private func carryOut(_ transition: SessionTransition) {
        guard let session else { return }
        asked = nil

        Task {
            switch transition {
            case .skip: await store.skip(session)
            case .delete: await store.delete(session)
            // Starting has its own button on this screen, which opens the logging surface as well.
            case .start: return
            }
            if store.action.refusal == nil { dismiss() }
        }
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
                .disabled(store.isWorkoutInFlight || store.action.isRunning)

                Button(role: .destructive) {
                    asked = .skip
                } label: {
                    Label(SessionTransition.skip.label, systemImage: SessionTransition.skip.systemImage)
                }
                .disabled(store.action.isRunning)
            } footer: {
                if store.isWorkoutInFlight {
                    Text("Finish the workout you are logging before starting another.")
                } else {
                    Text("Starting it records the session on today, whatever day it was planned for.")
                }
            }
        }
        .listStyle(.insetGrouped)
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
}
