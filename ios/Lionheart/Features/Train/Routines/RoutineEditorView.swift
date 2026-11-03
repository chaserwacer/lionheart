import SwiftUI

/// Which routine the editor is open on.
enum RoutineEditing: Identifiable {
    case new
    case existing(Routine)

    var id: String {
        switch self {
        case .new: "new"
        case .existing(let routine): routine.routineId.uuidString
        }
    }
}

/// A routine's name, notes and movements, each with its number of working sets.
///
/// Movements are reordered by dragging, removed by swiping and added from the same picker the
/// logging surface uses. There are no weights here, on purpose: a workout opens each set with what
/// was done last time.
struct RoutineEditorView: View {
    let editing: RoutineEditing
    let routines: RoutineStore
    let library: MovementLibraryStore

    @Environment(\.dismiss) private var dismiss
    @State private var name: String
    @State private var notes: String
    @State private var movements: [RoutineMovement]
    @State private var isPickerPresented = false

    init(editing: RoutineEditing, routines: RoutineStore, library: MovementLibraryStore) {
        self.editing = editing
        self.routines = routines
        self.library = library
        switch editing {
        case .new:
            _name = State(initialValue: "")
            _notes = State(initialValue: "")
            _movements = State(initialValue: [])
        case .existing(let routine):
            _name = State(initialValue: routine.name)
            _notes = State(initialValue: routine.notes)
            _movements = State(initialValue: routine.movements)
        }
    }

    private var content: RoutineContent {
        RoutineContent(
            name: name.trimmingCharacters(in: .whitespacesAndNewlines),
            notes: notes.trimmingCharacters(in: .whitespacesAndNewlines),
            movements: movements)
    }

    private var isSavable: Bool { !content.name.isEmpty && !movements.isEmpty && !routines.action.isRunning }

    var body: some View {
        NavigationStack {
            List {
                Section {
                    TextField("Name", text: $name)
                        .textInputAutocapitalization(.words)
                    TextField("Notes", text: $notes, axis: .vertical)
                        .lineLimit(1...4)
                }

                Section {
                    ForEach($movements) { $movement in
                        Stepper(value: Binding(
                            get: { movement.workingSets },
                            set: { movement = RoutineMovement(movementDataId: movement.movementDataId, workingSets: $0) }),
                                in: RoutineMovement.setRange) {
                            LabeledContent(library.fullName(of: movement.movementDataId)) {
                                Text(Formatting.count(movement.workingSets, "set")).monospacedDigit()
                            }
                        }
                    }
                    .onMove { movements.move(fromOffsets: $0, toOffset: $1) }
                    .onDelete { movements.remove(atOffsets: $0) }

                    Button("Add a movement") { isPickerPresented = true }
                } header: {
                    Text("Movements")
                } footer: {
                    Text("Working sets only. Warm-ups are offered when you train it.")
                }

                if let refusal = routines.action.refusal {
                    Section {
                        Text(refusal.message).font(.footnote).foregroundStyle(.orange)
                    }
                }
            }
            .listStyle(.insetGrouped)
            .environment(\.editMode, .constant(.active))
            .navigationTitle(isNew ? "New routine" : "Edit routine")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") {
                        routines.dismissRefusal()
                        dismiss()
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Save") { Task { await save() } }
                        .fontWeight(.semibold)
                        .disabled(!isSavable)
                }
            }
            .sheet(isPresented: $isPickerPresented) {
                MovementPicker(library: library) { movement in
                    guard !movements.contains(where: { $0.movementDataId == movement.movementDataId }) else { return }
                    movements.append(RoutineMovement(movementDataId: movement.movementDataId, workingSets: 2))
                }
            }
            .task { await library.refreshIfNeeded() }
        }
    }

    private var isNew: Bool {
        if case .new = editing { return true }
        return false
    }

    private func save() async {
        let saved: Routine? = switch editing {
        case .new: await routines.create(content)
        case .existing(let routine): await routines.replace(routine.routineId, with: content)
        }
        if saved != nil { dismiss() }
    }
}
