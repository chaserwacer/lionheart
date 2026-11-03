import SwiftUI

/// Correcting a finished session: its sets, when it happened, and its notes.
///
/// The sets are corrected with Train's own pieces — `EditSetSheet` for a set already there, the
/// same `SetEntryForm` for a new one, `MovementPicker` for a movement — so a set reads and types the
/// same whether it is logged live or put right a week later.
///
/// Nothing is sent until Save, which restates the session whole. A correction needs the server:
/// a change to the sets or the start re-detects every record over the history, which only the
/// server can do and which it may refuse. So with no connection the sheet says so and keeps the
/// edit, rather than queueing a correction the server might later turn down with nobody watching.
struct SessionRevisionView: View {
    let history: HistoryStore
    let library: MovementLibraryStore
    /// Run after a save the server took, for a screen holding its own copy of the session.
    let afterSaving: () async -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var draft: SessionRevisionDraft
    @State private var editing: SessionRevisionDraft.RevisedSet?
    @State private var adding: SessionRevisionDraft.Block?
    @State private var isPickingMovement = false
    @State private var isSaving = false
    @State private var failure: LoadFailure?
    @State private var isDiscardAsked = false

    init(
        draft: SessionRevisionDraft, history: HistoryStore, library: MovementLibraryStore,
        afterSaving: @escaping () async -> Void = {}
    ) {
        self.history = history
        self.library = library
        self.afterSaving = afterSaving
        _draft = State(initialValue: draft)
    }

    var body: some View {
        NavigationStack {
            List {
                if let failure { failureSection(failure) }
                whenSection
                ForEach(draft.blocks) { block in
                    blockSection(block)
                }
                Section {
                    Button("Add movement") { isPickingMovement = true }
                }
                notesSection
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            .environment(\.defaultMinListRowHeight, 34)
            .navigationTitle("Edit session")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .interactiveDismissDisabled(draft.hasChanges || isSaving)
            .confirmationDialog(
                "Discard these changes?", isPresented: $isDiscardAsked, titleVisibility: .visible
            ) {
                Button("Discard changes", role: .destructive) { dismiss() }
            }
            .sheet(item: $editing) { set in
                EditSetSheet(command: set.command, library: library) { command in
                    draft.replace(set.id, with: command)
                }
            }
            .sheet(item: $adding) { block in
                NewSetSheet(
                    title: library.title(of: block.movementDataId),
                    draft: draft.newSetDraft(for: block)
                ) { command in
                    draft.append(command, to: block)
                }
            }
            .sheet(isPresented: $isPickingMovement) {
                MovementPicker(library: library) { movement in
                    draft.addMovement(movement.movementDataId)
                }
            }
        }
    }

    // MARK: - Toolbar

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .cancellationAction) {
            Button("Cancel") {
                if draft.hasChanges { isDiscardAsked = true } else { dismiss() }
            }
            .disabled(isSaving)
        }
        ToolbarItem(placement: .confirmationAction) {
            if isSaving {
                ProgressView()
            } else {
                Button("Save") { save() }
                    .disabled(!draft.isSavable)
            }
        }
    }

    private func save() {
        isSaving = true
        failure = nil
        let draft = draft
        Task {
            defer { isSaving = false }
            do {
                try await history.revise(draft)
                await afterSaving()
                dismiss()
            } catch let refusal as LoadFailure {
                failure = refusal
            } catch {
                // Cancelled: the sheet is already going away.
            }
        }
    }

    // MARK: - Sections

    private func failureSection(_ failure: LoadFailure) -> some View {
        Section {
            Text(Self.sentence(for: failure))
                .foregroundStyle(.orange)
        }
    }

    /// What a failed save says. With no server the edit is kept and can simply be sent again: a
    /// correction restates the whole session, so one that did land is not doubled by a second.
    private static func sentence(for failure: LoadFailure) -> String {
        switch failure {
        case .offline:
            "Not saved: the server could not be reached, and editing a past session needs it. The changes are still here; saving again is safe."
        case .notFound:
            "Not saved: this session is no longer on the server."
        case .refused(let reason), .unavailable(let reason):
            "Not saved. \(reason)"
        }
    }

    private var whenSection: some View {
        Section {
            DatePicker(
                draft.end == nil ? "At" : "Started",
                selection: Binding(get: { draft.start }, set: { draft.moveStart(to: $0) }),
                in: ...Date())
            if let end = draft.end {
                DatePicker(
                    "Finished",
                    selection: Binding(get: { end }, set: { draft.setEnd($0) }),
                    in: draft.start...)
                LabeledContent("Duration") {
                    Text(Formatting.duration(seconds: end.timeIntervalSince(draft.start))).monospacedDigit()
                }
            }
        } header: {
            Text("When")
        } footer: {
            Text(whenFooter)
        }
    }

    private var whenFooter: String {
        let moving = "Moving the start moves the finish with it."
        let records = draft.reachesRecords
            ? " Saving re-detects your personal records over the whole history, because these sets or this start have changed."
            : ""
        return (draft.end == nil ? "The session is recorded on this day." : moving) + records
    }

    @ViewBuilder
    private func blockSection(_ block: SessionRevisionDraft.Block) -> some View {
        let numbers = SetKind.numbers(of: block.sets.map(\.command.kind))
        Section {
            ForEach(Array(block.sets.enumerated()), id: \.element.id) { position, set in
                Button { editing = set } label: {
                    SetRow(number: numbers[position], command: set.command, target: nil, record: .notApplicable)
                        .foregroundStyle(Color(.label))
                }
                .swipeActions(edge: .trailing) {
                    Button("Delete", systemImage: "trash", role: .destructive) {
                        draft.remove(set.id)
                    }
                }
                .accessibilityHint("Edits the set")
            }
            if block.sets.isEmpty, let movement = block.movementDataId {
                Button("Remove movement", role: .destructive) { draft.dropMovement(movement) }
            }
            Button("Add set") { adding = block }
        } header: {
            Text(block.movementDataId == nil ? "Other work" : library.title(of: block.movementDataId))
        } footer: {
            if block.sets.isEmpty {
                Text("Not saved until it has a set.")
            }
        }
    }

    private var notesSection: some View {
        Section("Notes") {
            TextField("Notes", text: $draft.notes, prompt: Text("How it went"), axis: .vertical)
                .lineLimit(2...10)
        }
    }
}

/// One new set for a session being corrected, typed into the logging surface's own row.
private struct NewSetSheet: View {
    let title: String
    let add: (SetCommand) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var draft: SetDraft

    init(title: String, draft: SetDraft, add: @escaping (SetCommand) -> Void) {
        self.title = title
        self.add = add
        _draft = State(initialValue: draft)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section(title) {
                    SetEntryForm(draft: $draft, actionLabel: "Add") { command in
                        add(command)
                        dismiss()
                    }
                }
            }
            .navigationTitle("New set")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
            }
            .presentationDetents([.medium, .large])
        }
    }
}
