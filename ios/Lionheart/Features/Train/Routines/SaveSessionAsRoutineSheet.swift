import SwiftUI

/// Saving a past session as a routine: pick the session, name it, done.
///
/// The fastest way to make one, because the athlete has already trained the session they mean.
/// The server takes the movements in the order they were trained and each one's working sets, and
/// the session becomes the routine's first, so the next workout from it has something to compare
/// with.
struct SaveSessionAsRoutineSheet: View {
    /// Recently completed sessions, most recent first.
    let sessions: [TrainingSession.Completed]
    let routines: RoutineStore
    let library: MovementLibraryStore

    @Environment(\.dismiss) private var dismiss
    @State private var chosen: UUID?
    @State private var name = ""

    var body: some View {
        NavigationStack {
            List {
                if sessions.isEmpty {
                    ContentUnavailableView {
                        Text("No recent sessions")
                    } description: {
                        Text("Sessions completed in the last 30 days are listed here.")
                    }
                    .listRowBackground(Color.clear)
                } else {
                    Section {
                        TextField("Upper, Lower, Full body", text: $name)
                            .textInputAutocapitalization(.words)
                    } header: {
                        Text("Name")
                    }
                    Section {
                        ForEach(sessions, id: \.envelope.trainingSessionId) { session in
                            row(session)
                        }
                    } header: {
                        Text("Session")
                    } footer: {
                        Text("Its movements and working sets, in the order trained. Warm-ups are left out.")
                    }
                }
                if let refusal = routines.action.refusal {
                    Section {
                        Text(refusal.message).font(.footnote).foregroundStyle(.orange)
                    }
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Routine from a session")
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
                        .disabled(chosen == nil || trimmedName.isEmpty || routines.action.isRunning)
                }
            }
        }
    }

    private var trimmedName: String { name.trimmingCharacters(in: .whitespacesAndNewlines) }

    private func row(_ session: TrainingSession.Completed) -> some View {
        let id = session.envelope.trainingSessionId
        let movements = Self.movementOrder(of: session)
        let names = movements.prefix(4).map { library.fullName(of: $0) }.joined(separator: ", ")
        let more = movements.count > 4 ? " +\(movements.count - 4)" : ""
        let workingSets = session.sets.count(where: \.kind.isWork)

        return Button {
            chosen = id
        } label: {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(Formatting.date(session.envelope.date))
                        .font(.headline)
                        .foregroundStyle(Color(.label))
                    Text("\(names)\(more) · \(Formatting.count(workingSets, "set"))")
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                        .lineLimit(2)
                }
                Spacer(minLength: 8)
                if chosen == id {
                    Image(systemName: "checkmark")
                        .foregroundStyle(Tokens.accent)
                        .accessibilityLabel("Chosen")
                }
            }
        }
    }

    /// The movements in the order the session first trained each.
    private static func movementOrder(of session: TrainingSession.Completed) -> [UUID] {
        session.sets.compactMap(\.movementDataId).reduce(into: []) { order, id in
            if !order.contains(id) { order.append(id) }
        }
    }

    private func save() async {
        guard let chosen else { return }
        if await routines.create(named: trimmedName, fromSession: chosen) != nil { dismiss() }
    }
}
