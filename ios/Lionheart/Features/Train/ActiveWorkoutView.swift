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
    /// The athlete's bodyweight, for the load a weighted dip moved.
    let bodyweight: BodyweightStore
    /// The routines, for the finish readout's comparison with last time.
    let routines: RoutineStore
    /// Hands the finish readout to whoever presents it once this surface has gone.
    let finished: (WorkoutSummary) -> Void
    /// The log and the focus list, for a movement's page opened from its section.
    let history: HistoryStore
    let focus: FocusStore
    /// The going-in state and what is next, for the header.
    let goingIn: GoingInStore
    /// The priority lifts, which lead the Add sheet.
    let balance: TrainingBalanceStore

    @Environment(\.dismiss) private var dismiss

    /// The entry row's fields. One at a time: the athlete is working through one movement, and two
    /// open rows would make "which set does Log log?" a question.
    @State private var draft: SetDraft?
    @State private var isAdding = false
    @State private var isFinishPresented = false
    @State private var isGoingInShown = false
    @State private var isDiscardAsked = false
    @State private var editing: LoggedSet?
    /// The set whose deletion is waiting on a yes. Hung on the surface rather than on a row,
    /// because a dialog per set is a dialog whose owner SwiftUI picks.
    @State private var deleting: LoggedSet?
    /// The movement sheet asked for from a section's menu: its warm-up ramp, or its setup.
    @State private var movementSheet: MovementSheet?
    /// A movement's page pushed over the surface — its history and records, mid-workout.
    @State private var route: [HistoryRoute] = []

    private enum MovementSheet: Identifiable {
        case warmUp(movementDataId: UUID, working: Double, unit: WeightUnit)
        case setup(movementDataId: UUID)

        var id: String {
            switch self {
            case .warmUp(let id, _, _): "warm-up \(id)"
            case .setup(let id): "setup \(id)"
            }
        }
    }

    /// How long a rest to start after a set. The athlete's own preference, so it lives with the
    /// view and is handed to the store when a set is logged — the store does not read preferences.
    @AppStorage("lh.rest.seconds") private var restSeconds = 120.0
    @AppStorage("lh.rest.automatic") private var startsRestAutomatically = true

    var body: some View {
        NavigationStack(path: $route) {
            Group {
                if let workout = store.workout {
                    surface(workout, reading: WorkoutReading.of(workout))
                } else {
                    // The workout finished — most likely from the Finish sheet — and this sheet is
                    // on its way out. A spinner is less jarring than an empty list for one frame.
                    ProgressView().controlSize(.large)
                }
            }
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .navigationDestination(for: HistoryRoute.self) { route in
                HistoryDestination(route: route, history: history, focus: focus, library: library)
            }
            .task {
                openFocusedMovement()
                // The header's reads beside the library's rather than after it: the header is the
                // first thing on screen, and the library is usually remembered already.
                async let header: Void = refreshHeader()
                await library.refreshIfNeeded()
                reseedUntouchedRow()
                await bodyweight.refreshIfNeeded()
                if store.workout?.routine != nil { await routines.refreshIfNeeded() }
                await header
            }
            .onChange(of: store.workout == nil) { _, gone in
                if gone { dismiss() }
            }
        }
        .sheet(isPresented: $isAdding) {
            AddWorkSheet(library: library, priorityIds: priorityIds) { add($0) }
        }
        .sheet(isPresented: $isGoingInShown) {
            if let state = goingIn.state.value {
                GoingInSheet(state: state)
            }
        }
        .sheet(isPresented: $isFinishPresented) {
            FinishWorkoutSheet(
                store: store, library: library, bodyweight: bodyweight, routines: routines,
                finished: finished)
        }
        .sheet(item: $movementSheet) { sheet in
            switch sheet {
            case .warmUp(let movementDataId, let working, let unit):
                WarmUpSheet(
                    title: library.title(of: movementDataId),
                    ramp: WarmUpRamp.to(working, unit: unit, loading: loading(of: movementDataId, in: unit)),
                    working: working,
                    plates: library.movement(movementDataId)?.isBarbell == true ? PlateSet.for(unit) : nil
                ) { step in
                    logWarmUp(step, on: movementDataId, unit: unit)
                }
            case .setup(let movementDataId):
                MovementSetupSheet(
                    title: library.title(of: movementDataId),
                    setup: library.movement(movementDataId)?.setup ?? .none
                ) { setup in
                    try await library.updateSetup(setup, of: movementDataId)
                }
            }
        }
        .sheet(item: $editing) { logged in
            EditSetSheet(command: logged.command, library: library) { command in
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
        // A set is work the athlete did, typed in a gym, and `PUT /sets` replaces the whole list —
        // so a deleted set is gone on the server too, with nothing to undo it. The ask is one tap
        // on an action taken once or twice a session.
        .confirmationDialog(
            "Delete this set?",
            isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }),
            titleVisibility: .visible,
            presenting: deleting
        ) { logged in
            Button("Delete set", role: .destructive) {
                store.remove(logged.id)
                deleting = nil
            }
        } message: { logged in
            if let numbers = Formatting.set(logged.command) {
                Text("\(numbers). This cannot be undone.")
            } else {
                Text("This cannot be undone.")
            }
        }
    }

    /// The elapsed clock, in the title's place and ticking.
    ///
    /// It has to be here rather than in `navigationTitle`, which takes a string evaluated once: a
    /// clock has to be a `TimelineView` to tick, and this is the only placement that puts one where
    /// a title goes. It is also the only elapsed clock the athlete can see while this sheet is up,
    /// since the sheet is over the workout bar.
    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .principal) {
            VStack(spacing: 0) {
                if let workout = store.workout, workout.destination.isLive {
                    TimelineView(.periodic(from: .now, by: 1)) { context in
                        Text(Formatting.clock(seconds: workout.elapsed(at: context.date)))
                            .font(.headline.monospacedDigit())
                            .accessibilityLabel(
                                "\(Formatting.duration(seconds: workout.elapsed(at: context.date))) elapsed")
                    }
                } else {
                    Text("Past workout").font(.headline)
                }
                if let routine = store.workout?.routine {
                    Text(routine.name)
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                        .lineLimit(1)
                }
            }
        }
        ToolbarItem(placement: .topBarLeading) {
            Button("Close") { dismiss() }
        }
        // Add is what the surface is for between sets, so it has the primary place; Finish happens
        // once a session and lives in the menu, behind the same sheet it always opened. On iOS 26
        // adjacent items share one glass capsule, so the spacer keeps Add reading as its own control.
        ToolbarItem(placement: .topBarTrailing) {
            Button {
                isAdding = true
            } label: {
                Label("Add", systemImage: "plus")
            }
            .fontWeight(.semibold)
            .disabled(store.workout == nil)
        }
        if #available(iOS 26.0, *) {
            ToolbarSpacer(.fixed, placement: .topBarTrailing)
        }
        ToolbarItem(placement: .topBarTrailing) {
            Menu("More", systemImage: "ellipsis.circle") {
                Button("Finish workout", systemImage: "flag.checkered") { isFinishPresented = true }
                    .disabled(store.workout == nil)
                if store.workout?.sync.isWorthPushing == true {
                    Button("Sync now", systemImage: "arrow.triangle.2.circlepath") {
                        Task { await store.syncNow() }
                    }
                }
                Divider()
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
        let header = goingInReading(for: workout, reading: reading)
        return ScrollViewReader { proxy in
            list(workout, reading: reading, header: header)
                // Every set logged adds a row above the entry row and pushes it down — under the
                // rest bar that appears at the same moment. Found by logging: the next tap on Log
                // landed on the bar. So the entry row is brought back above it, every set.
                // A beat later, not in the same update: the rest bar's inset arrives with the set,
                // and a scroll made before it is laid out lands the row under it all the same.
                .onChange(of: workout.sets.count) {
                    Task {
                        try? await Task.sleep(for: .milliseconds(200))
                        withAnimation(.snappy) { proxy.scrollTo(Self.entryRow, anchor: .bottom) }
                    }
                }
                // And to a row opened from the Add sheet, whose section is new at the bottom.
                .onChange(of: openedOn) {
                    withAnimation(.snappy) { proxy.scrollTo(Self.entryRow, anchor: .bottom) }
                }
        }
    }

    /// The entry row's identity in the list, wherever it is open, for scrolling to it.
    private static let entryRow = "entry-row"

    /// What the entry row is open on — a movement, or a shape with none — so opening it on
    /// something else can be noticed. Logging another set on the same movement is not a change.
    private var openedOn: String? {
        draft.map { "\($0.movementDataId?.uuidString ?? "none") \($0.modality.rawValue)" }
    }

    private func list(_ workout: ActiveWorkout, reading: WorkoutReading, header: GoingInReading?) -> some View {
        List {
            if let header { goingInSection(header) }
            if workout.sync.needsSaying { syncSection(workout.sync) }
            if !reading.totals.isEmpty { totalsSection(reading.totals) }

            ForEach(reading.movements) { movement in
                movementSection(movement, in: workout, header: header)
            }

            if !reading.unattached.isEmpty || !reading.looseTargets.isEmpty || isOpenOnUnattached {
                otherSection(reading)
            }

            if reading.movements.isEmpty, reading.unattached.isEmpty, !isOpenOnUnattached {
                emptySection
            }
            noteSection(workout)
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .environment(\.defaultMinListRowHeight, 34)
        .animation(.snappy, value: workout.sets.count)
        .safeAreaInset(edge: .bottom, spacing: 0) {
            if workout.rest.isResting(at: Date()) {
                RestTimerBar(
                    rest: workout.rest,
                    next: nextUp,
                    standard: restSeconds,
                    extend: { store.extendRest(by: $0) },
                    skip: { store.skipRest() },
                    choose: { chooseRest($0) })
            }
        }
    }

    // MARK: - Going in

    /// The header's reading, for a workout being trained now. One written down afterwards was
    /// trained on another day, which today's state says nothing about.
    private func goingInReading(for workout: ActiveWorkout, reading: WorkoutReading) -> GoingInReading? {
        guard workout.destination.isLive, let state = goingIn.state.value else { return nil }
        return GoingInReading.of(
            state, next: goingIn.next.value, movements: reading.movements.map(\.movementDataId),
            hasWork: workout.workSetCount > 0)
    }

    private func goingInSection(_ header: GoingInReading) -> some View {
        GoingInSection(
            reading: header, library: library, isStale: goingIn.state.isStale,
            openDetail: { isGoingInShown = true })
    }

    private func refreshHeader() async {
        guard store.workout?.destination.isLive == true else { return }
        await goingIn.refreshIfStale()
        await balance.refreshIfNeeded()
    }

    /// Each family's priority lift, push, pull and legs, for the Add sheet to lead with.
    private var priorityIds: [UUID] {
        balance.priorities.value?.families.compactMap { family in
            if case .chosen(let chosen) = family.lift { chosen.movementDataId } else { nil }
        } ?? []
    }

    // MARK: - Resting

    /// What the rest is before: the open row's numbers. Not the movement's name, which is the
    /// section right above and would push the numbers off a bar a third of the row wide.
    private var nextUp: String? {
        guard let draft, let command = draft.command, let numbers = Formatting.set(command) else { return nil }
        return "Next \(numbers)"
    }

    /// Makes `duration` the standard rest and moves the one running to it (`RestClock.resized`).
    private func chooseRest(_ duration: TimeInterval) {
        restSeconds = duration
        store.resizeRest(to: duration)
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
                // One figure in pounds: a session that mixed kilograms and pounds reads its whole volume.
                if let pounds = totals.poundsVolume {
                    StatTile(value: Formatting.volume(pounds, .pounds), label: "Volume")
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
        _ movement: WorkoutReading.MovementSets, in workout: ActiveWorkout, header: GoingInReading?
    ) -> some View {
        Section {
            // Where an active injury names this movement, said on the movement itself: the header
            // has scrolled away by the time the athlete reaches the third lift.
            ForEach(header?.injuries(touching: movement.movementDataId) ?? []) { injury in
                Text([injury.name, injury.lastPain.map { "pain \($0)/10" }].compactMap { $0 }.joined(separator: " · "))
                    .font(.footnote.weight(.semibold))
                    .foregroundStyle(.orange)
                    .accessibilityLabel("Active injury touches this movement: \(injury.name)")
            }
            if let setup = library.movement(movement.movementDataId)?.setup, setup.hasNotes {
                Text(setup.notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(4)
                    .onTapGesture { movementSheet = .setup(movementDataId: movement.movementDataId) }
                    .accessibilityLabel("Setup notes: \(setup.notes)")
            }
            if let previous = library.previous[movement.movementDataId] {
                lastTimeRow(previous)
            }

            ForEach(movement.sets) { numbered in
                loggedSetRow(
                    numbered.logged,
                    number: numbered.number,
                    target: numbered.target,
                    earlier: movement.sets.prefix { $0.id != numbered.id }.map(\.logged.command),
                    records: library.records(for: numbered.logged.command))
            }

            if let target = movement.nextTarget {
                LabeledContent("Next target", value: Formatting.target(target))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }

            if isOpen(on: movement.movementDataId), let open = Binding($draft) {
                SetEntryForm(
                    draft: open,
                    guide: SetGuide.of(
                        open.wrappedValue, in: workout,
                        previous: library.previous[movement.movementDataId],
                        movement: library.movement(movement.movementDataId),
                        bodyweight: bodyweight.series)
                ) { command in
                    log(command, on: movement.movementDataId)
                }
                .id(Self.entryRow)
            } else {
                Button {
                    open(movement.movementDataId)
                } label: {
                    Label(movement.sets.isEmpty ? "Log the first set" : "Add a set", systemImage: "plus")
                }
                .labelStyle(.titleOnly)
            }
        } header: {
            HStack {
                Text(library.title(of: movement.movementDataId))
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
                movementMenu(movement, in: workout)
            }
        }
    }

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
    private func movementMenu(_ movement: WorkoutReading.MovementSets, in workout: ActiveWorkout) -> some View {
        Menu {
            if let target = warmUpTarget(for: movement.movementDataId, in: workout) {
                Button("Warm-up sets") {
                    movementSheet = .warmUp(
                        movementDataId: movement.movementDataId, working: target.weight, unit: target.unit)
                }
            }
            Button("Setup and notes") {
                movementSheet = .setup(movementDataId: movement.movementDataId)
            }
            Button("History and records") {
                route.append(.movement(movement.movementDataId))
            }
            // The picker lists near-identical movements side by side, so a wrong pick is ordinary.
            // No confirmation: nothing is logged on it, and adding it back is a pick.
            if workout.canDrop(movement.movementDataId) {
                // The draft is left as it is. The entry row reads it through a force-unwrapped
                // binding, so clearing it in the update that removes this section crashes; once the
                // section is gone nothing shows it, and the next `open` replaces it.
                Button("Remove from workout", role: .destructive) { store.drop(movement.movementDataId) }
            }
        } label: {
            Image(systemName: "ellipsis.circle")
                .accessibilityLabel("\(library.title(of: movement.movementDataId)) options")
        }
        .textCase(nil)
    }

    /// The weight a warm-up ramp builds to: the entry row's, where it is open on this movement with a
    /// weight in it, else what the next working set would open with.
    private func warmUpTarget(
        for movementDataId: UUID, in workout: ActiveWorkout
    ) -> (weight: Double, unit: WeightUnit)? {
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

    private func loading(of movementDataId: UUID, in unit: WeightUnit) -> WarmUpRamp.Loading {
        let movement = library.movement(movementDataId)
        if movement?.setup.addsToBodyweight == true { return .addedToBodyweight }
        if movement?.isBarbell == true, let plates = PlateSet.for(unit) { return .barbell(plates) }
        return .other
    }

    /// Logs one step of the ramp as a warm-up. No rest is started: the rest that matters is the one
    /// after the work, and a two-minute clock after a set of ten with the empty bar is noise.
    private func logWarmUp(_ step: WarmUpRamp.Step, on movementDataId: UUID, unit: WeightUnit) {
        store.log(
            .lift(SetCommand.Lift(
                movementDataId: movementDataId, reps: step.reps, weight: step.weight, unit: unit,
                rpe: .unrated, kind: .warmUp)),
            restingFor: nil)
        Haptics.setLogged()
        library.noteUsed(movementDataId)
        // The entry row moves on to the work, which a warm-up never seeds.
        if draft?.movementDataId == movementDataId { open(movementDataId) }
    }

    /// Cardio and sets in words — the work no movement owns — with the entry row when it is open on
    /// one, under a header naming what is being logged.
    @ViewBuilder
    private func otherSection(_ reading: WorkoutReading) -> some View {
        Section {
            ForEach(Array(reading.unattached.enumerated()), id: \.element.id) { index, set in
                loggedSetRow(set, number: index + 1, target: nil, earlier: [], records: library.records(for: set.command))
            }
            ForEach(Array(reading.looseTargets.enumerated()), id: \.offset) { _, target in
                Label(Formatting.target(target), systemImage: "target")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                    .labelStyle(.titleOnly)
            }
            if isOpenOnUnattached, let open = Binding($draft) {
                SetEntryForm(draft: open) { command in
                    log(command, on: nil)
                }
                .id(Self.entryRow)
            }
        } header: {
            HStack {
                Text(unattachedTitle)
                Spacer()
                if isOpenOnUnattached {
                    Button("Done") { draft = nil }
                        .font(.footnote)
                        .textCase(nil)
                }
            }
        }
    }

    /// "Run", "Ride", "In words" while the row is open on one; "Other work" otherwise.
    private var unattachedTitle: String {
        guard isOpenOnUnattached, let draft else { return "Other work" }
        if draft.modality == .freeform { return "In words" }
        return CardioWork(draft)?.title ?? "Other work"
    }

    /// An unplanned workout before anything is in it: where Add and Finish are, said once.
    private var emptySection: some View {
        Section {} footer: {
            Text("Add a lift or cardio with + above. Finish is in the More menu.")
        }
    }

    @ViewBuilder
    private func noteSection(_ workout: ActiveWorkout) -> some View {
        if case .afterTheFact = workout.destination {
            Section {} footer: {
                Text("Nothing is sent until you finish. Choose Finish workout from the More menu to save the whole session.")
            }
        } else if workout.hasLeftItsPlan {
            Section {} footer: {
                Text("The planned session was started or skipped elsewhere, so this workout is saved as a session of its own.")
            }
        }
    }

    // MARK: - A set already logged

    /// One logged set, with the three things that can be done to it.
    ///
    /// A tap still opens the edit sheet, because that is the gesture that was already here and the
    /// one the athlete will try first. The long press reaches the same sheet in a named way and adds
    /// the two actions a sheet is overkill for: logging the same set again — three sets of five at
    /// the same weight is the commonest thing in a gym — and deleting it.
    private func loggedSetRow(
        _ logged: LoggedSet, number: Int, target: SetPrescription?, earlier: [SetCommand],
        records: SubjectRecords?
    ) -> some View {
        SetRow(
            number: number,
            command: logged.command,
            target: target,
            record: SetRecordOutcome.of(logged.command, after: earlier, against: records))
            .contentShape(.rect)
            .onTapGesture { editing = logged }
            .contextMenu {
                Button {
                    editing = logged
                } label: {
                    Label("Edit set", systemImage: "square.and.pencil")
                }
                Button {
                    repeatSet(logged)
                } label: {
                    Label("Log it again", systemImage: "plus.square.on.square")
                }
                Button(role: .destructive) {
                    deleting = logged
                } label: {
                    Label("Delete set", systemImage: "trash")
                }
            }
            .swipeActions(edge: .leading) {
                Button {
                    editing = logged
                } label: {
                    Label("Edit", systemImage: "square.and.pencil")
                }
                .tint(Tokens.accent)
            }
            .swipeActions(edge: .trailing) {
                Button(role: .destructive) {
                    deleting = logged
                } label: {
                    Label("Delete", systemImage: "trash")
                }
            }
    }

    /// Logs the same set again, exactly as the Log button would.
    ///
    /// The command carries its own movement, so this lands under the right header wherever the row
    /// was pressed, and the rest clock and the haptic are the same because this *is* a set being
    /// logged — a quieter version of it would make the athlete check whether it worked.
    private func repeatSet(_ logged: LoggedSet) {
        log(logged.command, on: logged.command.movementDataId)
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

    /// What a pick from the Add sheet does. A movement is added before its row opens: the row lives
    /// in the movement's section, and the section exists only once the workout holds the movement.
    private func add(_ work: AddedWork) {
        switch work {
        case .movement(let movement):
            store.add(movement.movementDataId)
            open(movement.movementDataId)
            library.noteUsed(movement.movementDataId)
        case .cardio(let cardio):
            guard let workout = store.workout else { return }
            draft = SetDraft(from: cardio.nextSet(in: workout))
            store.work(on: nil)
        case .words:
            draft = SetDraft(from: .freeform(text: ""))
            store.work(on: nil)
        }
    }

    /// Fills the entry row from last time when last time arrived after the row opened
    /// (`SetDraft.filledFromLastTime`). Found by relaunching mid-workout: the row opened empty.
    private func reseedUntouchedRow() {
        guard let draft, let workout = store.workout,
              let filled = draft.filledFromLastTime(draft.movementDataId.flatMap { library.previous[$0] }, in: workout)
        else { return }
        self.draft = filled
    }

    /// Opens the entry row on a movement, pre-filled by the carry-forward rule.
    private func open(_ movementDataId: UUID) {
        guard let workout = store.workout else { return }
        draft = SetDraft(next: movementDataId, in: workout, previous: library.previous[movementDataId])
        store.work(on: movementDataId)
    }

    /// Logs the set and leaves the row open with the same numbers in it.
    ///
    /// Leaving it open is the point: three sets of five at a hundred kilos is then three taps, and
    /// the athlete never has to find the button again. The rest clock starting at the same moment
    /// is what tells them it worked, along with the row appearing above.
    private func log(_ command: SetCommand, on movementDataId: UUID?) {
        let earlier = store.workout?.sets.map(\.command) ?? []
        store.log(command, restingFor: startsRestAutomatically ? restSeconds : nil)
        Haptics.setLogged()

        if let movementDataId {
            library.noteUsed(movementDataId)
        }
        if SetRecordOutcome.of(command, after: earlier, against: library.records(for: command))
            .isWorthSaying {
            Haptics.achievement()
        }

        // Re-seeded by the rule rather than kept as typed, so a correction made before logging does
        // not persist into the next set unless it was logged — and the next set takes last time's
        // reps at its own position.
        if let movementDataId, let workout = store.workout {
            draft = SetDraft(next: movementDataId, in: workout, previous: library.previous[movementDataId])
        } else {
            draft = SetDraft(from: command)
        }
    }
}
