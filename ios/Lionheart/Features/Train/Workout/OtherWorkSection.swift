import SwiftUI

/// Cardio and sets in words — the work no movement owns — with the entry row when it is open on
/// one, under a header naming what is being logged.
struct OtherWorkSection: View {
    let reading: WorkoutReading
    let library: MovementLibraryStore
    @Binding var draft: SetDraft?
    let rowActions: LoggedSetRow.Actions
    let log: (SetCommand) -> Void

    var body: some View {
        Section {
            ForEach(Array(reading.unattached.enumerated()), id: \.element.id) { index, set in
                LoggedSetRow(
                    logged: set, number: index + 1, target: nil,
                    record: SetRecordOutcome.of(set.command, after: [], against: library.records(for: set.command)),
                    actions: rowActions)
            }
            ForEach(Array(reading.looseTargets.enumerated()), id: \.offset) { _, target in
                Label(Formatting.target(target), systemImage: "target")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                    .labelStyle(.titleOnly)
            }
            if isOpen, let row = Binding($draft) {
                SetEntryForm(draft: row) { command in
                    log(command)
                }
                .id(ActiveWorkoutView.entryRow)
            }
        } header: {
            HStack {
                Text(title)
                Spacer()
                if isOpen {
                    Button("Done") { draft = nil }
                        .font(.footnote)
                        .textCase(nil)
                }
            }
        }
    }

    /// Whether the entry row is open on something no movement owns.
    private var isOpen: Bool {
        guard let draft else { return false }
        return draft.movementDataId == nil
    }

    /// "Run", "Ride", "In words" while the row is open on one; "Other work" otherwise.
    private var title: String {
        guard isOpen, let draft else { return "Other work" }
        if draft.modality == .freeform { return "In words" }
        return CardioWork(draft)?.title ?? "Other work"
    }
}
