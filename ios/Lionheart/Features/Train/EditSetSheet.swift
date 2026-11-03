import SwiftUI

/// Correcting a set already logged — in the workout in flight, or in a finished session being
/// edited.
///
/// A sheet rather than editing in place, which is the opposite of the choice made for logging a new
/// set — and for the opposite reason. Logging happens twenty times an hour and must be frictionless;
/// correcting a typed weight happens once or twice and benefits from being deliberate, with the
/// numbers it is replacing visible above the fields.
struct EditSetSheet: View {
    /// The set as it stands, which the fields open on and the footer quotes.
    let command: SetCommand
    let library: MovementLibraryStore
    let save: (SetCommand) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var draft: SetDraft

    init(
        command: SetCommand,
        library: MovementLibraryStore,
        save: @escaping (SetCommand) -> Void
    ) {
        self.command = command
        self.library = library
        self.save = save
        // Seeded from the set itself rather than from the carry-forward rule: this is the set being
        // changed, so its own numbers are what should be in the fields.
        _draft = State(initialValue: SetDraft(from: command))
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    SetEntryForm(draft: $draft, actionLabel: "Save") { command in
                        save(command)
                        dismiss()
                    }
                } header: {
                    Text(library.title(of: command.movementDataId))
                } footer: {
                    if let was = Formatting.set(command) {
                        Text("Logged as \(was).")
                    }
                }
            }
            .navigationTitle("Edit set")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                }
            }
            .presentationDetents([.medium, .large])
        }
    }
}
