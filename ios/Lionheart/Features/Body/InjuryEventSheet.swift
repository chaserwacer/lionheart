import SwiftUI

/// Logs a treatment or a check-in against one injury: which it was, how much it hurt, and what was
/// done or felt.
///
/// Pain is a stepper with the figure beside it rather than a slider, because a pain score is a whole
/// number the athlete chooses, not a position he drags to. The server stamps the event with its own
/// clock, so there is no date to choose.
struct InjuryEventSheet: View {
    let injury: Injury
    let store: BodyStore

    @Environment(\.dismiss) private var dismiss
    @State private var kind: InjuryEventDraft.Kind = .checkIn
    /// The pain the athlete has set, or `nil` before he touches it.
    @State private var chosenPain: Int?
    @State private var notes = ""
    @State private var isSaving = false
    @State private var refusal: LoadFailure?

    /// Starts at the last pain logged, since a check-in is most often "about the same".
    private var painLevel: Int { chosenPain ?? InjuryHistory.of(injury).latestPain ?? 0 }

    private var pain: Binding<Int> {
        Binding(get: { painLevel }, set: { chosenPain = $0 })
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("Kind", selection: $kind) {
                        ForEach(InjuryEventDraft.Kind.allCases) { kind in
                            Text(kind.label).tag(kind)
                        }
                    }
                    .pickerStyle(.segmented)
                    .listRowBackground(Color.clear)
                    .listRowInsets(EdgeInsets())
                }
                Section {
                    Stepper(value: pain, in: InjuryEventDraft.painScale) {
                        HStack(alignment: .firstTextBaseline) {
                            Text("Pain")
                            Spacer()
                            Text("\(painLevel)/10")
                                .font(.body.monospacedDigit().weight(.semibold))
                        }
                    }
                    .accessibilityValue("\(painLevel) out of 10")
                } footer: {
                    Text("0 is none, 10 the worst imaginable.")
                }
                Section("Notes") {
                    TextField("Notes", text: $notes, prompt: Text(prompt), axis: .vertical)
                        .lineLimit(3...8)
                }
                if let refusal {
                    Section {
                        Text(refusal.message).foregroundStyle(.orange)
                    }
                }
            }
            .navigationTitle(injury.name.nonEmpty ?? "Injury")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Log") { save() }
                    }
                }
            }
            .interactiveDismissDisabled(isSaving)
        }
        .presentationDetents([.medium, .large])
    }

    private var prompt: String {
        switch kind {
        case .checkIn: "How it feels, what aggravated it"
        case .treatment: "What was done, by whom"
        }
    }

    private func save() {
        let draft = InjuryEventDraft(kind: kind, painLevel: painLevel, notes: notes)
        isSaving = true
        refusal = nil
        Task {
            defer { isSaving = false }
            do {
                try await store.addEvent(draft, to: injury)
                dismiss()
            } catch let failure as LoadFailure {
                refusal = failure
            } catch {
                // Cancelled: the sheet is already going away.
            }
        }
    }
}
