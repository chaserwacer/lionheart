import SwiftUI

/// The athlete's setup for one movement: the cues shown while logging it, and whether its weight
/// is plate added to their bodyweight.
///
/// Saved to the server, which is where it lives — a seat position set on the phone has to be there
/// on the next one too — so a save with no connection is refused in the sheet, with the text kept.
struct MovementSetupSheet: View {
    let title: String
    let setup: MovementSetup
    let save: (MovementSetup) async throws -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var notes: String
    @State private var addsToBodyweight: Bool
    @State private var action: ActionState = .idle

    init(title: String, setup: MovementSetup, save: @escaping (MovementSetup) async throws -> Void) {
        self.title = title
        self.setup = setup
        self.save = save
        _notes = State(initialValue: setup.notes)
        _addsToBodyweight = State(initialValue: setup.addsToBodyweight)
    }

    private var edited: MovementSetup {
        MovementSetup(
            notes: notes.trimmingCharacters(in: .whitespacesAndNewlines), addsToBodyweight: addsToBodyweight)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Seat 4, pin 7, close grip", text: $notes, axis: .vertical)
                        .lineLimit(3...8)
                } header: {
                    Text("Notes")
                } footer: {
                    Text("Shown under the movement while you log it.")
                }
                Section {
                    Toggle("Weight is added to bodyweight", isOn: $addsToBodyweight)
                } footer: {
                    Text("For weighted dips and pull-ups: the load moved is your logged bodyweight plus the weight on the belt.")
                }
                if let refusal = action.refusal {
                    Section {
                        Text(refusal.message)
                            .font(.footnote)
                            .foregroundStyle(.orange)
                    }
                }
            }
            .navigationTitle(title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        Task { await commit() }
                    } label: {
                        if action.isRunning { ProgressView() } else { Text("Save").fontWeight(.semibold) }
                    }
                    .disabled(action.isRunning || edited == setup || notes.count > MovementSetup.notesLimit)
                }
            }
        }
    }

    private func commit() async {
        action = .running
        do {
            try await save(edited)
            dismiss()
        } catch {
            action = ActionState.refusal(from: error)
        }
    }
}
