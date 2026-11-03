import SwiftUI

/// Correcting a set already logged.
///
/// A sheet rather than editing in place, which is the opposite of the choice made for logging a new
/// set — and for the opposite reason. Logging happens twenty times an hour and must be frictionless;
/// correcting a typed weight happens once or twice and benefits from being deliberate, with the
/// numbers it is replacing visible above the fields.
struct EditSetSheet: View {
    let logged: LoggedSet
    let library: MovementLibraryStore
    let save: (SetCommand) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var draft: SetDraft

    init(
        logged: LoggedSet,
        library: MovementLibraryStore,
        save: @escaping (SetCommand) -> Void
    ) {
        self.logged = logged
        self.library = library
        self.save = save
        // Seeded from the set itself rather than from the carry-forward rule: this is the set being
        // changed, so its own numbers are what should be in the fields.
        _draft = State(initialValue: SetDraft(from: logged.command))
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
                    Text(library.title(of: logged.movementDataId))
                } footer: {
                    if let was = Formatting.set(logged.command) {
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
