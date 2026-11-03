import SwiftUI

/// The full-screen logging surface: the workout, set by set.
///
/// A `List` of sections, one per movement, because that is the shape of the thing being logged and
/// it is what Apple's own apps are made of — separators, Dynamic Type and swipe-to-delete all come
/// for free and all three are wanted here.
///
/// The screen is arranged around one claim: **logging the next set should cost one tap.** So the
/// entry row sits inside the movement's own section with the previous set's numbers already in it,
/// the Log button is the widest thing on the row, and nothing between the athlete and it needs a
/// decision. Everything else on the screen — targets, totals, the rest clock — is read-only until
/// touched.
struct ActiveWorkoutView: View {
    let store: ActiveWorkoutStore
    let library: MovementLibraryStore

    @Environment(\.dismiss) private var dismiss

    /// The entry row's fields. One at a time: the athlete is working through one movement, and two
    /// open rows would make "which set does Log log?" a question.
    @State private var draft: SetDraft?
    @State private var isPickerPresented = false
    @State private var isFinishPresented = false
    @State private var isDiscardAsked = false
    @State private var editing: LoggedSet?

    /// How long a rest to start after a set. The athlete's own preference, so it lives with the
    /// view and is handed to the store when a set is logged — the store does not read preferences.
    @AppStorage("lh.rest.seconds") private var restSeconds = 120.0
    @AppStorage("lh.rest.automatic") private var startsRestAutomatically = true

    var body: some View {
        NavigationStack {
            Group {
                if let workout = store.workout {
                    surface(workout, reading: WorkoutReading.of(workout))
                } else {
                    // The workout finished — most likely from the Finish sheet — and this cover is
                    // on its way out. A spinner is less jarring than an empty list for one frame.
                    ProgressView().controlSize(.large)
                }
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .task {
                openFocusedMovement()
                await library.refreshIfNeeded()
            }
            .onChange(of: store.workout == nil) { _, gone in
                if gone { dismiss() }
            }
        }
        .sheet(isPresented: $isPickerPresented) {
            MovementPicker(library: library) { movement in
                open(movement.movementDataId)
                library.noteUsed(movement.movementDataId)
            }
        }
        .sheet(isPresented: $isFinishPresented) {
            FinishWorkoutSheet(store: store, library: library)
        }
        .sheet(item: $editing) { logged in
            EditSetSheet(logged: logged, library: library) { command in
                store.restate(logged.id, as: command)
            }
        }
        .confirmationDialog(
            "Discard this workout?",
            isPresented: $isDiscardAsked,
            titleVisibility: .visible
        ) {
            Button("Discard workout", role: .destructive) {
                Task { await store.discard() }
            }
        } message: {
            Text("Every set you have logged will be deleted. This cannot be undone.")
        }
    }

    /// The elapsed clock, in the title's place and ticking.
    ///
    /// It has to be here rather than in `navigationTitle`, which takes a string evaluated once: a
    /// clock has to be a `TimelineView` to tick, and this is the only placement that puts one where
    /// a title goes. It is also the only elapsed clock the athlete can see while this cover is up,
    /// since the cover is over the workout bar.
    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .principal) {
            if let workout = store.workout, case .liveSession = workout.destination {
                TimelineView(.periodic(from: .now, by: 1)) { context in
                    Text(Formatting.clock(seconds: workout.elapsed(at: context.date)))
                        .font(.headline.monospacedDigit())
                        .accessibilityLabel(
                            "\(Formatting.duration(seconds: workout.elapsed(at: context.date))) elapsed")
                }
            } else {
                Text("Past workout").font(.headline)
            }
        }
        ToolbarItem(placement: .topBarLeading) {
            Button("Close") { dismiss() }
        }
        ToolbarItem(placement: .topBarTrailing) {
            Button("Finish") { isFinishPresented = true }
                .fontWeight(.semibold)
                .disabled(store.workout == nil)
        }
        // Finish ends the workout; the menu behind it holds a sync and a discard. On iOS 26
        // adjacent items share one glass capsule, and a committing action reading as part of the
        // same control as an overflow menu is how a workout gets discarded by accident.
        if #available(iOS 26.0, *) {
            ToolbarSpacer(.fixed, placement: .topBarTrailing)
        }
        ToolbarItem(placement: .topBarTrailing) {
            Menu("More", systemImage: "ellipsis.circle") {
                if store.workout?.sync.isWorthPushing == true {
                    Button("Sync now", systemImage: "arrow.triangle.2.circlepath") {
                        Task { await store.syncNow() }
                    }
                }
                Button(role: .destructive) {
                    isDiscardAsked = true
                } label: {
                    Label("Discard workout", systemImage: "trash")
                }
            }
        }
    }

    // MARK: - The surface

    private func surface(_ workout: ActiveWorkout, reading: WorkoutReading) -> some View {
        List {
            if workout.sync.needsSaying { syncSection(workout.sync) }
            if !reading.totals.isEmpty { totalsSection(reading.totals) }

            ForEach(reading.movements) { movement in
                movementSection(movement, in: workout)
            }

            if !reading.unattached.isEmpty || !reading.looseTargets.isEmpty {
                otherSection(reading)
            }

            addSection(workout)
        }
        .listStyle(.insetGrouped)
        .safeAreaInset(edge: .bottom, spacing: 0) {
            if workout.rest.isResting(at: Date()) {
                RestTimerBar(
                    rest: workout.rest,
                    extend: { store.extendRest(by: $0) },
                    skip: { store.skipRest() })
            }
        }
    }

    @ViewBuilder
    private func syncSection(_ sync: WorkoutSyncState) -> some View {
        Section {
            switch sync {
            case .synced, .pending:
                EmptyView()
            case .failed(let reason):
                VStack(alignment: .leading, spacing: 4) {
                    Label("Not synced yet", systemImage: "arrow.triangle.2.circlepath")
                        .font(.subheadline.weight(.semibold))
                    Text("Your sets are saved on this phone. \(reason)")
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                    Button("Try again") { Task { await store.syncNow() } }
                        .font(.footnote)
                }
            case .refused(let reason):
                VStack(alignment: .leading, spacing: 4) {
                    Label("The server will not take these sets", systemImage: "exclamationmark.triangle.fill")
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(.orange)
                    Text(reason)
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
    }

    private func totalsSection(_ totals: WorkoutTotals) -> some View {
        Section {
            HStack(alignment: .top, spacing: Tokens.cardPadding) {
                StatTile(value: "\(totals.setCount)", label: "Sets")
                if let weight = totals.weights.first {
                    StatTile(value: Formatting.volume(weight), label: "Volume")
                }
                if let distance = totals.distances.first {
                    StatTile(
                        value: Formatting.distance(distance.distance, distance.unit),
                        label: "Distance")
                }
            }
        }
    }

    @ViewBuilder
    private func movementSection(
        _ movement: WorkoutReading.MovementSets, in workout: ActiveWorkout
    ) -> some View {
        Section {
            ForEach(movement.sets) { numbered in
                SetRow(
                    number: numbered.number,
                    command: numbered.logged.command,
                    target: numbered.target,
                    record: SetRecordOutcome.of(
                        numbered.logged.command,
                        against: library.records(for: movement.movementDataId)))
                    .contentShape(.rect)
                    .onTapGesture { editing = numbered.logged }
                    .swipeActions(edge: .trailing) {
                        Button(role: .destructive) {
                            store.remove(numbered.logged.id)
                        } label: {
                            Label("Delete", systemImage: "trash")
                        }
                    }
            }

            if let target = movement.nextTarget {
                LabeledContent("Next target", value: Formatting.target(target))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }

            if isOpen(on: movement.movementDataId), let open = Binding($draft) {
                SetEntryForm(draft: open) { command in
                    log(command, on: movement.movementDataId)
                }
            } else {
                Button {
                    open(movement.movementDataId)
                } label: {
                    Label(movement.sets.isEmpty ? "Log the first set" : "Add a set", systemImage: "plus")
                }
            }
        } header: {
            HStack {
                Text(library.title(of: movement.movementDataId))
                Spacer()
                if !movement.targets.isEmpty {
                    Text("\(movement.sets.count) of \(movement.targets.count)")
                        .monospacedDigit()
                }
            }
        }
    }

    @ViewBuilder
    private func otherSection(_ reading: WorkoutReading) -> some View {
        Section("Other work") {
            ForEach(Array(reading.unattached.enumerated()), id: \.element.id) { index, set in
                SetRow(
                    number: index + 1,
                    command: set.command,
                    target: nil,
                    record: .notApplicable)
                    .contentShape(.rect)
                    .onTapGesture { editing = set }
                    .swipeActions(edge: .trailing) {
                        Button(role: .destructive) {
                            store.remove(set.id)
                        } label: {
                            Label("Delete", systemImage: "trash")
                        }
                    }
            }
            ForEach(Array(reading.looseTargets.enumerated()), id: \.offset) { _, target in
                Label(Formatting.target(target), systemImage: "target")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    @ViewBuilder
    private func addSection(_ workout: ActiveWorkout) -> some View {
        Section {
            Button {
                isPickerPresented = true
            } label: {
                Label("Add a movement", systemImage: "plus.circle")
            }

            // Both stay on screen while the entry row is open, so changing your mind about which
            // one you wanted is a tap rather than a dead end — there is nothing else here that
            // closes the row.
            Button {
                draft = SetDraft(from: SetDefaults.nextTrailRun(in: workout))
                store.work(on: nil)
            } label: {
                Label("Add a trail run", systemImage: "figure.run")
            }
            Button {
                draft = SetDraft(from: .freeform(text: ""))
                store.work(on: nil)
            } label: {
                Label("Log something in words", systemImage: "text.bubble")
            }

            if isOpenOnUnattached, let open = Binding($draft) {
                SetEntryForm(draft: open) { command in
                    log(command, on: nil)
                }
            }
        } footer: {
            if case .afterTheFact = workout.destination {
                Text("Nothing is sent until you finish. Tap Finish to save the whole session.")
            }
        }
    }

    // MARK: - Doing

    /// Whether the entry row is open on this movement.
    private func isOpen(on movementDataId: UUID) -> Bool {
        draft?.movementDataId == movementDataId
    }

    /// Whether the entry row is open on something no movement owns.
    private var isOpenOnUnattached: Bool {
        guard let draft else { return false }
        return draft.movementDataId == nil
    }

    /// Opens the entry row on the movement the athlete should be working through, when nothing is
    /// open yet.
    ///
    /// Starting a planned session therefore lands with the first prescribed movement's fields
    /// already filled from its target, which is the commonest way into this screen and the one
    /// tap it is worth saving. An athlete who wanted a different movement taps its own row.
    private func openFocusedMovement() {
        guard draft == nil, let workout = store.workout else { return }
        let reading = WorkoutReading.of(workout)
        guard let focus = reading.focus(given: workout.currentMovementDataId) else { return }
        open(focus)
    }

    /// Opens the entry row on a movement, pre-filled by the carry-forward rule.
    private func open(_ movementDataId: UUID) {
        guard let workout = store.workout else { return }
        draft = SetDraft(from: SetDefaults.nextSet(on: movementDataId, in: workout))
        store.work(on: movementDataId)
    }

    /// Logs the set and leaves the row open with the same numbers in it.
    ///
    /// Leaving it open is the point: three sets of five at a hundred kilos is then three taps, and
    /// the athlete never has to find the button again. The rest clock starting at the same moment
    /// is what tells them it worked, along with the row appearing above.
    private func log(_ command: SetCommand, on movementDataId: UUID?) {
        store.log(command, restingFor: startsRestAutomatically ? restSeconds : nil)
        Haptics.setLogged()

        if let movementDataId {
            library.noteUsed(movementDataId)
            if let records = library.records(for: movementDataId),
               SetRecordOutcome.of(command, against: records).isWorthSaying {
                Haptics.achievement()
            }
        }

        // Re-seeded from the set just logged rather than kept as typed, so a correction made before
        // logging does not persist into the next set unless it was logged.
        draft = SetDraft(from: command)
    }
}
