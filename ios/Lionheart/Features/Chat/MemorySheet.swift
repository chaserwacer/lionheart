import SwiftUI

/// What Lionheart remembers from the thread, in the athlete's hands.
///
/// Memory is the premise of every new session, so it has to be inspectable and correctable: each
/// memory shows where it came from in his own words, what it said before it was revised, and can be
/// corrected or forgotten. Grouped by kind, standing kinds first, because those reach every session.
struct MemorySheet: View {
    let store: MemoryStore

    @Environment(\.dismiss) private var dismiss
    @State private var pendingDeletion: ChatMemory?

    var body: some View {
        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    LoadingRow()
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let memories):
                    content(memories)
                case .stale(let memories, let staleness):
                    StaleBanner(staleness)
                    content(memories)
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Memory")
            .navigationBarTitleDisplayMode(.inline)
            .navigationDestination(for: UUID.self) { memoryId in
                MemoryDetail(store: store, memoryId: memoryId)
            }
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
            .refreshable { await store.refresh() }
            .confirmationDialog(
                "Forget this memory?",
                isPresented: Binding(
                    get: { pendingDeletion != nil },
                    set: { if !$0 { pendingDeletion = nil } }),
                titleVisibility: .visible,
                presenting: pendingDeletion
            ) { memory in
                Button("Forget", role: .destructive) {
                    pendingDeletion = nil
                    Task { await store.delete(memory) }
                }
            } message: { _ in
                Text("It and every earlier version of it. No later session will be given it.")
            }
            .alert(
                "That memory was not forgotten",
                isPresented: Binding(
                    get: { store.action.refusal != nil },
                    set: { if !$0 { store.dismissRefusal() } })
            ) {
                Button("OK") { store.dismissRefusal() }
            } message: {
                if let refusal = store.action.refusal { Text(refusal.message) }
            }
        }
        .task { await store.refresh() }
    }

    @ViewBuilder
    private func content(_ memories: [ChatMemory]) -> some View {
        if memories.isEmpty {
            Section {
                ContentUnavailableView {
                    Label("Nothing remembered yet", systemImage: "note.text")
                } description: {
                    Text("A session is distilled into memory three hours after its last message.")
                }
                .listRowBackground(Color.clear)
            }
        } else {
            ForEach(MemorySection.of(memories)) { section in
                Section {
                    ForEach(section.memories) { memory in
                        NavigationLink(value: memory.memoryId) {
                            MemoryRow(memory: memory)
                        }
                        .swipeActions {
                            Button("Forget", systemImage: "trash", role: .destructive) {
                                pendingDeletion = memory
                            }
                        }
                    }
                } header: {
                    Text(section.title)
                } footer: {
                    if section.kind.isStanding {
                        Text("Given to every session.")
                    }
                }
            }
            Section {
            } footer: {
                Text("Distilled from the thread when a session ends, three hours after its last message. Goals, injuries and preferences go into every session; the rest are recalled when a session bears on them. A correction applies from the next session.")
            }
        }
    }

    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await store.refresh() } }
            }
        }
        .listRowBackground(Color.clear)
    }
}

private struct MemoryRow: View {
    let memory: ChatMemory

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(memory.text)
                .font(.body)
                .foregroundStyle(Color(.label))
            Text(MemoryWords.provenance(memory))
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }
}

/// One memory: what it says, the words it came from, and what it said before.
private struct MemoryDetail: View {
    let store: MemoryStore

    /// The version on screen. A correction replaces the memory with a new version under a new id,
    /// and the screen follows it rather than finding the old one gone.
    @State private var memoryId: UUID
    @State private var isEditing = false

    init(store: MemoryStore, memoryId: UUID) {
        self.store = store
        _memoryId = State(initialValue: memoryId)
    }

    private var memory: ChatMemory? { store.state.value?.first { $0.memoryId == memoryId } }

    var body: some View {
        Group {
            if let memory {
                List {
                    Section {
                        Text(memory.text)
                            .font(.body)
                            .textSelection(.enabled)
                        LabeledContent("Kind", value: MemorySection.name(of: memory.kind))
                        LabeledContent("Recorded", value: MemoryWords.provenance(memory))
                    }
                    if !memory.quotes.isEmpty {
                        Section("Said") {
                            ForEach(memory.quotes) { quote in
                                VStack(alignment: .leading, spacing: 3) {
                                    Text(quote.said)
                                        .font(.callout)
                                    Text(MemoryWords.quoteLine(quote))
                                        .font(.caption)
                                        .foregroundStyle(Color(.secondaryLabel))
                                }
                            }
                        }
                    }
                    if !memory.earlier.isEmpty {
                        Section("Before") {
                            ForEach(Array(memory.earlier.enumerated()), id: \.offset) { _, version in
                                VStack(alignment: .leading, spacing: 3) {
                                    Text(version.text)
                                        .font(.callout)
                                        .foregroundStyle(Color(.secondaryLabel))
                                    Text(MemoryWords.versionLine(version))
                                        .font(.caption)
                                        .foregroundStyle(Color(.tertiaryLabel))
                                }
                            }
                        }
                    }
                }
                .listStyle(.insetGrouped)
                .toolbar {
                    ToolbarItem(placement: .topBarTrailing) {
                        Button("Correct") { isEditing = true }
                    }
                }
                .sheet(isPresented: $isEditing) {
                    MemoryEditor(store: store, memory: memory) { corrected in memoryId = corrected.memoryId }
                }
            } else {
                // Forgotten or replaced while this was open; the list behind it has moved on.
                ContentUnavailableView("No longer remembered", systemImage: "note.text")
            }
        }
        .navigationTitle(memory.map { MemorySection.name(of: $0.kind) } ?? "Memory")
        .navigationBarTitleDisplayMode(.inline)
    }
}

/// The athlete's correction: what the memory should say instead, and of what kind.
private struct MemoryEditor: View {
    let store: MemoryStore
    let memory: ChatMemory
    let onCorrected: (ChatMemory) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var kind: MemoryKind
    @State private var text: String
    @State private var isSaving = false
    @State private var refusal: LoadFailure?

    init(store: MemoryStore, memory: ChatMemory, onCorrected: @escaping (ChatMemory) -> Void) {
        self.store = store
        self.memory = memory
        self.onCorrected = onCorrected
        _kind = State(initialValue: MemoryKind.known.contains(memory.kind) ? memory.kind : .fact)
        _text = State(initialValue: memory.text)
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    TextField("What Lionheart should remember", text: $text, axis: .vertical)
                        .lineLimit(3...8)
                } footer: {
                    Text("The current wording is kept as history.")
                }
                Picker("Kind", selection: $kind) {
                    ForEach(MemoryKind.known, id: \.self) { kind in
                        Text(MemorySection.name(of: kind)).tag(kind)
                    }
                }
                if let refusal {
                    Section {
                        Text(refusal.message).foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle("Correct memory")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") { save() }
                        .disabled(!canSave)
                }
            }
            .interactiveDismissDisabled(isSaving)
        }
        .presentationDetents([.medium, .large])
    }

    private var trimmed: String { text.trimmingCharacters(in: .whitespacesAndNewlines) }

    private var canSave: Bool {
        !isSaving && !trimmed.isEmpty && trimmed.count <= MemoryWords.maxLength
            && (trimmed != memory.text || kind != memory.kind)
    }

    private func save() {
        isSaving = true
        Task {
            switch await store.correct(memory, kind: kind, text: trimmed) {
            case .success(let corrected):
                onCorrected(corrected)
                dismiss()
            case .failure(let failure):
                refusal = failure
            }
            isSaving = false
        }
    }
}
