import SwiftUI

/// What a movement trains, on its own page: its pattern and muscles, and where that came from — the
/// athlete, the shipped map, or a judgement call the map marks unsure — with the edit beside it.
///
/// It describes the base, so it reads the same on every variation: incline bench and dumbbell bench
/// are both horizontal pushes. The edit sheet is the page's to present, since a sheet hung on a list
/// section is torn down with the rows it was attached to.
struct ClassificationSection: View {
    let movementDataId: UUID
    let library: MovementLibraryStore
    let edit: (MovementData) -> Void

    var body: some View {
        if let movement = library.movement(movementDataId) {
            Section {
                switch movement.classification {
                case .classified(let trains, let source):
                    LabeledContent("Pattern", value: trains.pattern.name)
                    LabeledContent("Primary", value: Self.names(trains.primary))
                    if !trains.secondary.isEmpty {
                        LabeledContent("Secondary", value: Self.names(trains.secondary))
                    }
                    Text(Self.source(source, family: trains.family))
                        .font(.footnote)
                        .foregroundStyle(Self.isUnsure(source) ? .orange : Color(.secondaryLabel))
                case .unclassified:
                    Text("Not classified, so its sets count toward no family, pattern or muscle.")
                        .foregroundStyle(Color(.secondaryLabel))
                case .unrecognised:
                    Text("Not readable by this version.")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } header: {
                Text("Trains")
            } footer: {
                Button(movement.classification.classification == nil ? "Classify" : "Edit classification") { edit(movement) }
                    .font(.footnote)
                    .disabled(movement.movementBaseId == nil)
            }
        }
    }

    private static func names(_ muscles: [MuscleGroup]) -> String {
        muscles.map(\.name).joined(separator: ", ")
    }

    private static func isUnsure(_ source: ClassificationSource) -> Bool {
        if case .mapUnsure = source { return true }
        return false
    }

    private static func source(_ source: ClassificationSource, family: PatternFamily) -> String {
        let counts = "Counts toward \(family.name.lowercased())."
        switch source {
        case .athlete: return "Stated by you. \(counts)"
        case .map: return "From the movement-pattern map. \(counts)"
        case .mapUnsure(let reason): return "From the map, unsure: \(reason) Confirm or correct it. \(counts)"
        case .unrecognised: return counts
        }
    }
}

/// Stating what a movement base trains: its pattern, then each muscle as primary or secondary.
struct ClassificationSheet: View {
    let movement: MovementData
    let library: MovementLibraryStore

    @Environment(\.dismiss) private var dismiss
    @Environment(TrainingBalanceStore.self) private var balance
    @State private var draft = ClassificationDraft(nil)
    @State private var hasLoaded = false
    @State private var isSaving = false
    @State private var refusal: String?

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Picker("Pattern", selection: $draft.pattern) {
                        ForEach(MovementPattern.known, id: \.wire) { Text($0.name).tag($0) }
                    }
                } footer: {
                    Text("Applies to every variation of \(movement.baseName).")
                }
                muscleSection(.primary)
                muscleSection(.secondary)
                if case .classified(_, let source) = movement.classification, source == .athlete {
                    Section {
                        Button("Use the map's classification") { Task { await withdraw() } }
                            .disabled(isSaving)
                    } footer: {
                        Text("Withdraws yours; the shipped map answers for \(movement.baseName) again.")
                    }
                }
                if let refusal {
                    Section { Text(refusal).foregroundStyle(.red) }
                }
            }
            .navigationTitle(movement.baseName)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") { Task { await save() } }
                        .disabled(draft.classification == nil || isSaving)
                }
            }
            .onAppear {
                guard !hasLoaded else { return }
                hasLoaded = true
                draft = ClassificationDraft(movement.classification.classification)
            }
        }
    }

    private func muscleSection(_ role: ClassificationDraft.Role) -> some View {
        Section {
            ForEach(MuscleGroup.known, id: \.wire) { muscle in
                Button {
                    draft.toggle(muscle, as: role)
                } label: {
                    HStack {
                        Text(muscle.name).foregroundStyle(Color(.label))
                        Spacer()
                        if draft.role(of: muscle) == role {
                            Image(systemName: "checkmark").foregroundStyle(Tokens.accent)
                        }
                    }
                }
                .accessibilityAddTraits(draft.role(of: muscle) == role ? .isSelected : [])
            }
        } header: {
            Text(role == .primary ? "Primary" : "Secondary")
        } footer: {
            Text(role == .primary
                 ? "A set counts whole for each. The first names the family of an isolation movement."
                 : "A set counts half for each.")
        }
    }

    private func save() async {
        guard let classification = draft.classification, let movementBaseId = movement.movementBaseId else { return }
        isSaving = true
        defer { isSaving = false }
        do {
            try await library.classify(base: movementBaseId, as: classification)
            await balance.refresh()
            dismiss()
        } catch {
            refusal = ActionState.refusal(from: error).refusal?.message ?? "Not saved."
        }
    }

    private func withdraw() async {
        guard let movementBaseId = movement.movementBaseId else { return }
        isSaving = true
        defer { isSaving = false }
        do {
            try await library.withdrawClassification(base: movementBaseId)
            await balance.refresh()
            dismiss()
        } catch {
            refusal = ActionState.refusal(from: error).refusal?.message ?? "Not withdrawn."
        }
    }
}
