import SwiftUI

/// The readout of a workout just finished: what it came to, against the last session of its routine
/// and against last time on each movement.
///
/// Terse on purpose — figures and their changes, no congratulation. It is also where a workout
/// becomes a routine, or updates the one it came from, since this is the moment the athlete knows
/// whether today's order is the one they want to keep.
struct WorkoutSummaryView: View {
    let summary: WorkoutSummary
    let library: MovementLibraryStore
    let routines: RoutineStore

    @Environment(\.dismiss) private var dismiss
    @State private var routineName = ""
    /// What became of the routine write asked for here, said in the routine section.
    @State private var routineOutcome: RoutineOutcome?

    private enum RoutineOutcome {
        case saved(String)
        case updated(String)
    }

    var body: some View {
        NavigationStack {
            List {
                figuresSection
                if let comparison = summary.comparison { comparisonSection(comparison) }
                if !summary.movements.isEmpty { movementsSection }
                routineSection
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            .navigationTitle(summary.routine?.name ?? "Workout")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }.fontWeight(.semibold)
                }
            }
            .alert(
                "Refused",
                isPresented: Binding(
                    get: { routines.action.refusal != nil },
                    set: { if !$0 { routines.dismissRefusal() } })
            ) {
                Button("OK") { routines.dismissRefusal() }
            } message: {
                if let refusal = routines.action.refusal { Text(refusal.message) }
            }
        }
    }

    // MARK: - Figures

    private var figuresSection: some View {
        Section {
            SummaryFigures(figures: [
                ("Duration", Formatting.duration(seconds: summary.duration)),
                ("Sets", "\(summary.totals.setCount)"),
            ] + summary.totals.weights.prefix(1).map { ("Volume", Formatting.volume($0)) }
              + [("Records", "\(summary.recordSets)")])
        } footer: {
            Text(summary.startedAt.formatted(date: .abbreviated, time: .shortened))
        }
    }

    private func comparisonSection(_ comparison: WorkoutSummary.Comparison) -> some View {
        Section {
            if let before = comparison.duration {
                compared("Duration", now: Formatting.duration(seconds: summary.duration),
                         change: Formatting.durationChange(seconds: summary.duration - before))
            }
            compared("Working sets", now: "\(summary.totals.setCount)",
                     change: Formatting.signedCount(summary.totals.setCount - comparison.totals.setCount))
            ForEach(summary.totals.weights) { weight in
                if let before = comparison.totals.weights.first(where: { $0.unit == weight.unit }), before.volume > 0 {
                    compared("Volume", now: Formatting.volume(weight),
                             change: Formatting.percentChange(weight.volume / before.volume - 1))
                }
            }
        } header: {
            Text("Against \(Formatting.date(comparison.date))")
        }
    }

    private func compared(_ label: String, now: String, change: String) -> some View {
        LabeledContent(label) {
            HStack(spacing: 8) {
                Text(now).foregroundStyle(Color(.label))
                Text(change).foregroundStyle(Color(.secondaryLabel))
                    .frame(minWidth: 56, alignment: .trailing)
            }
            .font(.body.monospacedDigit())
        }
    }

    // MARK: - Movements

    private var movementsSection: some View {
        Section {
            ForEach(summary.movements) { line in
                movementRow(line)
            }
        } header: {
            Text("Best set against last time")
        } footer: {
            Text("Change is in estimated one-rep max: for a weight added to bodyweight, on bodyweight plus plate where both days have a bodyweight logged.")
        }
    }

    private func movementRow(_ line: WorkoutSummary.MovementLine) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(library.fullName(of: line.movementDataId))
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(2)
                Spacer(minLength: 8)
                if let best = line.best {
                    Text(Self.topSet(best))
                        .font(.body.monospacedDigit())
                }
            }
            HStack(alignment: .firstTextBaseline) {
                Text(Formatting.count(line.workingSets, "working set"))
                Spacer(minLength: 8)
                if let previous = line.previousBest {
                    let change = line.change.map { " · \(Formatting.percentChange($0))" } ?? ""
                    Text("last \(Self.topSet(previous))\(change)")
                } else {
                    Text("first time")
                }
            }
            .font(.footnote.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
        }
        .accessibilityElement(children: .combine)
    }

    private static func topSet(_ set: WorkoutSummary.TopSet) -> String {
        "\(Formatting.weight(set.weight, set.unit)) × \(set.reps)"
    }

    // MARK: - Routine

    @ViewBuilder
    private var routineSection: some View {
        if let outcome = routineOutcome {
            Section {
                switch outcome {
                case .saved(let name):
                    Text("Saved as \(name).").foregroundStyle(Color(.secondaryLabel))
                case .updated(let name):
                    Text("\(name) updated.").foregroundStyle(Color(.secondaryLabel))
                }
            }
        } else if let routine = summary.routine {
            if summary.departsFromRoutine, let held = routines.routine(routine.routineId) {
                Section {
                    Button("Update \(held.name)") { Task { await update(held) } }
                        .disabled(routines.action.isRunning)
                } footer: {
                    Text("Today's movements and working sets differ from the routine. Updating it keeps today's order.")
                }
            }
        } else if !summary.routineMovements.isEmpty {
            Section {
                TextField("Upper, Lower, Full body", text: $routineName)
                    .textInputAutocapitalization(.words)
                Button("Save as routine") { Task { await save() } }
                    .disabled(trimmedName.isEmpty || routines.action.isRunning)
            } header: {
                Text("Routine")
            } footer: {
                Text("Its movements and working sets, in today's order. Weights come from last time when you start it.")
            }
        }
    }

    private var trimmedName: String { routineName.trimmingCharacters(in: .whitespacesAndNewlines) }

    private func save() async {
        let name = trimmedName
        let created = if let sessionId = summary.completedSessionId {
            await routines.create(named: name, fromSession: sessionId)
        } else {
            await routines.create(RoutineContent(name: name, notes: "", movements: summary.routineMovements))
        }
        if created != nil { routineOutcome = .saved(name) }
    }

    private func update(_ routine: Routine) async {
        let updated = await routines.replace(
            routine.routineId,
            with: RoutineContent(name: routine.name, notes: routine.notes, movements: summary.routineMovements))
        if updated != nil { routineOutcome = .updated(routine.name) }
    }
}
