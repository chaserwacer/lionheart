import SwiftUI

/// Pinning a movement, or revising a pin: the horizon, the sessions a week, and an optional target.
///
/// The horizon is offered as lengths — four weeks to a year — because that is how a block is
/// thought about, with its own dates beneath for a horizon that does not fit one. A refusal is
/// stated here, under the form, since an alert from the screen underneath has nowhere to present
/// while the sheet is up.
struct FocusPinSheet: View {
    let editing: Editing
    let focus: FocusStore
    let library: MovementLibraryStore

    /// What the sheet was opened for.
    enum Editing: Identifiable {
        /// A new pin, on a movement already chosen or one to choose here.
        case pinning(movementDataId: UUID?, name: String?)
        case revising(FocusPin)

        var id: String {
            switch self {
            case .pinning(let id, _): "pin-\(id?.uuidString ?? "new")"
            case .revising(let pin): "revise-\(pin.focusPinId.uuidString)"
            }
        }
    }

    @Environment(\.dismiss) private var dismiss

    @State private var movement: (id: UUID, name: String)?
    @State private var starts = Date()
    @State private var length: Length = .preset(.eightWeeks)
    @State private var customEnds = Date()
    @State private var sessionsPerWeek = 2
    @State private var hasTarget = false
    @State private var targetWeight = ""
    @State private var targetUnit: WeightUnit = .pounds
    @State private var targetReps = 5
    @State private var isChoosingMovement = false
    @State private var isSaving = false
    @State private var refusal: LoadFailure?
    @State private var hasLoaded = false

    /// A preset length or dates of the athlete's own.
    enum Length: Hashable {
        case preset(FocusLength)
        case custom
    }

    var body: some View {
        NavigationStack {
            Form {
                movementSection
                horizonSection
                Section {
                    Stepper(value: $sessionsPerWeek, in: FocusDraft.sessionsPerWeekRange) {
                        LabeledContent("Sessions a week") {
                            Text("\(sessionsPerWeek)").monospacedDigit()
                        }
                    }
                } footer: {
                    Text("What the horizon asks for. The focus list counts the sessions done against it.")
                }
                targetSection
                if case .revising(let pin) = editing, pin.isPinned {
                    Section {
                        Button("Unpin today") {
                            Task {
                                if let failure = await focus.unpinFromSheet(pin) {
                                    refusal = failure
                                } else {
                                    dismiss()
                                }
                            }
                        }
                        .disabled(isSaving)
                    } footer: {
                        Text("Kept as a past focus, with what it came to.")
                    }
                }
                if let refusal {
                    Section {
                        Text(refusal.message).foregroundStyle(.red)
                    }
                }
            }
            .navigationTitle(isRevising ? "Edit focus" : "Pin to focus")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(isRevising ? "Save" : "Pin") { Task { await save() } }
                        .disabled(draft == nil || movement == nil || isSaving)
                }
            }
            .sheet(isPresented: $isChoosingMovement) {
                MovementPicker(library: library) { chosen in
                    movement = (chosen.movementDataId, chosen.displayName)
                    isChoosingMovement = false
                }
            }
            .onAppear(perform: loadOnce)
        }
    }

    private var isRevising: Bool {
        if case .revising = editing { true } else { false }
    }

    // MARK: - Sections

    @ViewBuilder
    private var movementSection: some View {
        Section {
            switch editing {
            case .revising:
                // A pin's movement is fixed for its life: a different movement is a different focus.
                LabeledContent("Movement", value: movement?.name ?? "")
            case .pinning(let preset, _):
                if preset != nil {
                    LabeledContent("Movement", value: movement?.name ?? "")
                } else {
                    Button {
                        isChoosingMovement = true
                    } label: {
                        LabeledContent("Movement") {
                            Text(movement?.name ?? "Choose")
                                .foregroundStyle(movement == nil ? Tokens.accent : Color(.secondaryLabel))
                        }
                    }
                    .foregroundStyle(Color(.label))
                }
            }
        }
    }

    private var horizonSection: some View {
        Section {
            DatePicker("Starts", selection: $starts, displayedComponents: .date)
            Picker("Length", selection: $length) {
                ForEach(FocusLength.allCases) { Text($0.label).tag(Length.preset($0)) }
                Text("Custom").tag(Length.custom)
            }
            switch length {
            case .preset:
                LabeledContent("Ends", value: Formatting.date(endsDay))
            case .custom:
                DatePicker("Ends", selection: $customEnds, in: starts..., displayedComponents: .date)
            }
        } header: {
            Text("Horizon")
        }
    }

    @ViewBuilder
    private var targetSection: some View {
        Section {
            Toggle("Target", isOn: $hasTarget.animation())
            if hasTarget {
                LabeledContent("Load") {
                    HStack(spacing: 8) {
                        TextField("0", text: $targetWeight)
                            .keyboardType(.decimalPad)
                            .multilineTextAlignment(.trailing)
                            .monospacedDigit()
                        Picker("Unit", selection: $targetUnit) {
                            Text("lb").tag(WeightUnit.pounds)
                            Text("kg").tag(WeightUnit.kilograms)
                        }
                        .pickerStyle(.segmented)
                        .fixedSize()
                    }
                }
                Stepper(value: $targetReps, in: FocusDraft.targetRepsRange) {
                    LabeledContent("Reps") {
                        Text("\(targetReps)").monospacedDigit()
                    }
                }
            }
        } footer: {
            if hasTarget {
                Text("The load as you record it for this movement — added weight, for a weighted dip. Met by a set at the load for the reps or more inside the horizon.")
            }
        }
    }

    // MARK: - The draft

    private var startsDay: CalendarDay { CalendarDay(starts) }

    private var endsDay: CalendarDay {
        switch length {
        case .preset(let preset): preset.ends(startingOn: startsDay)
        case .custom: CalendarDay(customEnds)
        }
    }

    private var target: FocusTarget {
        guard hasTarget else { return .none }
        let weight = Double(targetWeight.replacingOccurrences(of: ",", with: ".")) ?? -1
        return .lift(weight: weight, unit: targetUnit, reps: targetReps)
    }

    private var draft: FocusDraft? {
        try? FocusDraft.make(
            starts: startsDay, ends: endsDay, sessionsPerWeek: sessionsPerWeek, target: target)
    }

    private func save() async {
        guard let draft, let movement else { return }
        isSaving = true
        defer { isSaving = false }
        let failure = switch editing {
        case .pinning: await focus.pin(movement.id, draft)
        case .revising(let pin): await focus.revise(pin, to: draft)
        }
        if let failure {
            refusal = failure
        } else {
            dismiss()
        }
    }

    /// Fills the form from what it was opened for, once — `onAppear` runs again when the movement
    /// picker closes over it.
    private func loadOnce() {
        guard !hasLoaded else { return }
        hasLoaded = true
        switch editing {
        case .pinning(let id, let name):
            if let id { movement = (id, name ?? library.catalogue.name(for: id)) }
            if let unit = id.flatMap(lastUnit) { targetUnit = unit }
        case .revising(let pin):
            movement = (pin.movementDataId, library.catalogue.name(for: pin.movementDataId))
            starts = pin.horizon.starts.middayDate()
            customEnds = pin.horizon.ends.middayDate()
            length = FocusLength.allCases
                .first { $0.ends(startingOn: pin.horizon.starts) == pin.horizon.ends }
                .map(Length.preset) ?? .custom
            sessionsPerWeek = pin.sessionsPerWeek
            if case .lift(let weight, let unit, let reps) = pin.target {
                hasTarget = true
                targetWeight = Formatting.measure(weight)
                targetUnit = unit
                targetReps = reps
            }
        }
    }

    /// The unit the movement's standing records are held in, so a target starts in the unit the
    /// athlete lifts it in.
    private func lastUnit(_ movementDataId: UUID) -> WeightUnit? {
        library.records(on: movementDataId)?.records.compactMap(\.achievement.heldIn).first
    }
}
