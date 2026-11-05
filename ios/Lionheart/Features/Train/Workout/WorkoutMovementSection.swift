import SwiftUI

/// One movement on the logging surface: what bears on it, last time, the sets logged so far, the
/// next target, and the entry row when it is open here.
///
/// The entry row lives inside the movement's own section with the next set's numbers already in it,
/// so logging the next set costs one tap and never a decision about where it goes.
struct WorkoutMovementSection: View {
    let movement: WorkoutReading.MovementSets
    let workout: ActiveWorkout
    /// The going-in reading, for the injuries that touch this movement.
    let header: GoingInReading?
    let library: MovementLibraryStore
    /// For the system load of a movement whose weight is added to bodyweight.
    let bodyweight: BodyweightStore
    @Binding var draft: SetDraft?
    @Binding var movementSheet: WorkoutMovementSheet?
    @Binding var route: [HistoryRoute]
    let rowActions: LoggedSetRow.Actions
    /// Opens the entry row on a movement, pre-filled by the carry-forward rule.
    let open: (UUID) -> Void
    let log: (SetCommand, UUID) -> Void
    let drop: (UUID) -> Void

    var body: some View {
        Section {
            // Where an active injury names this movement, said on the movement itself: the header
            // has scrolled away by the time the athlete reaches the third lift.
            ForEach(header?.injuries(touching: movementDataId) ?? []) { injury in
                Text([injury.name, injury.lastPain.map { "pain \($0)/10" }].compactMap { $0 }.joined(separator: " · "))
                    .font(.footnote.weight(.semibold))
                    .foregroundStyle(.orange)
                    .accessibilityLabel("Active injury touches this movement: \(injury.name)")
            }
            if let setup = library.movement(movementDataId)?.setup, setup.hasNotes {
                Text(setup.notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(4)
                    .onTapGesture { movementSheet = .setup(movementDataId: movementDataId) }
                    .accessibilityLabel("Setup notes: \(setup.notes)")
            }
            if let previous = library.previous[movementDataId] {
                lastTimeRow(previous)
            }

            ForEach(movement.sets) { numbered in
                LoggedSetRow(
                    logged: numbered.logged,
                    number: numbered.number,
                    target: numbered.target,
                    record: SetRecordOutcome.of(
                        numbered.logged.command,
                        after: movement.sets.prefix { $0.id != numbered.id }.map(\.logged.command),
                        against: library.records(for: numbered.logged.command)),
                    actions: rowActions)
            }

            if let target = movement.nextTarget {
                LabeledContent("Next target", value: Formatting.target(target))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }

            if draft?.movementDataId == movementDataId, let row = Binding($draft) {
                SetEntryForm(
                    draft: row,
                    guide: SetGuide.of(
                        row.wrappedValue, in: workout,
                        previous: library.previous[movementDataId],
                        movement: library.movement(movementDataId),
                        bodyweight: bodyweight.series)
                ) { command in
                    log(command, movementDataId)
                }
                .id(ActiveWorkoutView.entryRow)
            } else {
                Button {
                    open(movementDataId)
                } label: {
                    Label(movement.sets.isEmpty ? "Log the first set" : "Add a set", systemImage: "plus")
                }
                .labelStyle(.titleOnly)
            }
        } header: {
            HStack {
                Text(library.title(of: movementDataId))
                Spacer()
                switch movement.progress {
                case .unplanned:
                    EmptyView()
                case .within(let logged, let planned):
                    Text("\(logged) of \(planned)")
                        .monospacedDigit()
                        .accessibilityLabel("\(logged) of \(planned) planned sets")
                case .beyond(let planned, let extra):
                    Text("\(planned) of \(planned) +\(extra)")
                        .monospacedDigit()
                        .accessibilityLabel("All \(planned) planned sets, and \(extra) more")
                }
                movementMenu
            }
        }
    }

    private var movementDataId: UUID { movement.movementDataId }

    /// Last time on this movement, as one line: the day and every set, warm-ups marked.
    private func lastTimeRow(_ previous: PreviousPerformance) -> some View {
        let sets = previous.sets.compactMap { entry -> String? in
            guard let text = Formatting.lastTime(entry) else { return nil }
            return entry.kind.isWork ? text : "W \(text)"
        }
        return LabeledContent {
            Text(sets.joined(separator: ", "))
                .font(.footnote.monospacedDigit())
                .multilineTextAlignment(.trailing)
        } label: {
            Text("Last · \(Formatting.dayInline(previous.date))")
                .font(.footnote)
        }
        .foregroundStyle(Color(.secondaryLabel))
        .accessibilityElement(children: .combine)
    }

    /// What can be done to a movement as a whole: warm up to it, keep notes on it, or take it out
    /// of a workout it was added to by mistake.
    private var movementMenu: some View {
        Menu {
            if let target = warmUpTarget {
                Button("Warm-up sets") {
                    movementSheet = .warmUp(movementDataId: movementDataId, working: target.weight, unit: target.unit)
                }
            }
            Button("Setup and notes") {
                movementSheet = .setup(movementDataId: movementDataId)
            }
            Button("History and records") {
                route.append(.movement(movementDataId))
            }
            // The picker lists near-identical movements side by side, so a wrong pick is ordinary.
            // No confirmation: nothing is logged on it, and adding it back is a pick.
            if workout.canDrop(movementDataId) {
                // The draft is left as it is. The entry row reads it through a force-unwrapped
                // binding, so clearing it in the update that removes this section crashes; once the
                // section is gone nothing shows it, and the next `open` replaces it.
                Button("Remove from workout", role: .destructive) { drop(movementDataId) }
            }
        } label: {
            Image(systemName: "ellipsis.circle")
                .accessibilityLabel("\(library.title(of: movementDataId)) options")
        }
        .textCase(nil)
    }

    /// The weight a warm-up ramp builds to: the entry row's, where it is open on this movement with a
    /// weight in it, else what the next working set would open with.
    private var warmUpTarget: (weight: Double, unit: WeightUnit)? {
        if let draft, draft.movementDataId == movementDataId, draft.modality == .lift,
           let weight = SetDraft.number(draft.weight), weight > 0 {
            return (weight, draft.weightUnit)
        }
        guard case .lift(let next) = SetDefaults.nextSet(
            on: movementDataId, in: workout, previous: library.previous[movementDataId]),
              next.weight > 0
        else { return nil }
        return (next.weight, next.unit)
    }
}

/// The sheet a movement's menu asks the surface for: its warm-up ramp, or its setup.
enum WorkoutMovementSheet: Identifiable {
    case warmUp(movementDataId: UUID, working: Double, unit: WeightUnit)
    case setup(movementDataId: UUID)

    var id: String {
        switch self {
        case .warmUp(let id, _, _): "warm-up \(id)"
        case .setup(let id): "setup \(id)"
        }
    }
}
