import SwiftUI

/// Starts tracking an injury: what it is, when it started, and anything worth writing down.
///
/// The sheet stays up until the server has the injury, and states a refusal in place — the
/// athlete's words are still in the form, and an alert on the tab underneath could not present
/// over a sheet anyway.
struct NewInjurySheet: View {
    let store: BodyStore

    @Environment(\.dismiss) private var dismiss
    @State private var name = ""
    @State private var startedOn = Date()
    @State private var notes = ""
    @State private var isSaving = false
    @State private var refusal: LoadFailure?
    @FocusState private var isNameFocused: Bool

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("Name", text: $name, prompt: Text("Left patellar tendon"))
                        .focused($isNameFocused)
                        .textInputAutocapitalization(.sentences)
                    DatePicker("Started", selection: $startedOn, in: ...Date(), displayedComponents: .date)
                }
                Section("Notes") {
                    TextField("Notes", text: $notes, prompt: Text("Mechanism, location, what aggravates it"), axis: .vertical)
                        .lineLimit(3...8)
                }
                if let refusal {
                    Section {
                        Text(refusal.message).foregroundStyle(.orange)
                    }
                }
            }
            .navigationTitle("New injury")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Add") { save() }
                            .disabled(draft == nil)
                    }
                }
            }
            .interactiveDismissDisabled(isSaving)
            .onAppear { isNameFocused = true }
        }
    }

    private var draft: InjuryDraft? {
        let today = CalendarDay(Date())
        return try? InjuryDraft.make(
            name: name, startedOn: CalendarDay(startedOn), notes: notes, today: today).get()
    }

    private func save() {
        guard let draft else { return }
        isSaving = true
        refusal = nil
        Task {
            defer { isSaving = false }
            do {
                try await store.createInjury(draft)
                dismiss()
            } catch let failure as LoadFailure {
                refusal = failure
            } catch {
                // Cancelled: the sheet is already going away.
            }
        }
    }
}
