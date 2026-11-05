import SwiftUI

/// The one-field path: type what you did and let interpretation do the rest.
///
/// A sheet with its own `NavigationStack` and toolbar, hung on the screen rather than on a row, as
/// the conventions table says. It is short, so it gets a medium detent.
struct QuickLogSheet: View {
    let store: TodayStore

    @Environment(\.dismiss) private var dismiss
    @State private var text = ""
    @FocusState private var isFieldFocused: Bool

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField(
                        "What did you do?",
                        text: $text,
                        prompt: Text("Sled pull and push, worked up to 235, 3 sets"),
                        axis: .vertical)
                        .lineLimit(3...6)
                        .focused($isFieldFocused)
                        .textInputAutocapitalization(.sentences)
                        .disabled(store.quickLog.isInterpreting)
                } footer: {
                    Text("Loose is fine. The words are kept whatever Tempo makes of them.")
                }

                if store.quickLog.hasOutcome {
                    outcomeSection
                }
            }
            .navigationTitle("Quick log")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                        .disabled(store.quickLog.isInterpreting)
                }
                ToolbarItem(placement: .topBarTrailing) {
                    if store.quickLog.isInterpreting {
                        ProgressView()
                    } else {
                        Button("Log") { Task { await store.submitQuickLog(text) } }
                            .disabled(text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                    }
                }
            }
            .onChange(of: text) { _, _ in
                // A refusal describes what was typed, so it stops being true the moment that
                // changes.
                if case .refused = store.quickLog { store.dismissQuickLogOutcome() }
            }
        }
        // The field is focused here, unlike on the sign-in screen: the athlete opened this sheet to
        // type, so the keyboard is what they asked for.
        .presentationDetents([.medium, .large])
        .task { isFieldFocused = true }
        // The entry landing is the moment worth feeling; a refusal is read, not felt.
        .sensoryFeedback(.success, trigger: store.quickLog) { _, new in
            if case .committed = new { true } else { false }
        }
    }

    @ViewBuilder
    private var outcomeSection: some View {
        switch store.quickLog {
        case .committed(let sessions, let contexts, let warnings):
            Section {
                Label(Formatting.logged(sessions: sessions, contexts: contexts), systemImage: "checkmark.circle.fill")
                    .foregroundStyle(.green)
                ForEach(warnings, id: \.self) { warning in
                    Text(warning)
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
                Button("Done") { dismiss() }
            }

        case .needsConfirmation(let references):
            Section {
                Label("Tempo needs to know which movement you meant", systemImage: "questionmark.circle")
                ForEach(references) { reference in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(reference.rawName).font(.subheadline.weight(.semibold))
                        Text(candidateSummary(reference))
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            } footer: {
                // Honest about the gap rather than pretending the entry landed: the confirmation
                // round is a screen that does not exist yet, and the server has written nothing.
                Text("Choosing between them is not built yet, so nothing was saved. Logging it on the web app will work.")
            }

        case .refused(let failure):
            Section {
                Label(failure.message, systemImage: "exclamationmark.triangle.fill")
                    .foregroundStyle(.orange)
                if failure.isWorthRetrying {
                    Button("Try again") { Task { await store.submitQuickLog(text) } }
                }
            }

        case .idle, .interpreting:
            EmptyView()
        }
    }

    private func candidateSummary(_ reference: UnresolvedReference) -> String {
        guard !reference.candidates.isEmpty else { return "No close match" }
        return reference.candidates.prefix(3).map(\.name).joined(separator: ", ")
    }
}
