import SwiftUI

/// Let me do it.
///
/// The tab opens on its hero — where the week stands, as the training score and its partition, and
/// what to train next in a line — because those two are what every session on this tab is chosen
/// by and moves. The start section sits directly under it: start a workout or log one already done.
/// Then what is next in full, each with its aims and reasons; the score part by part; the week's
/// volume across push, pull and legs; the priorities. Routines, bodyweight and the plan follow,
/// there when wanted.
struct TrainView: View {
    let store: TrainStore
    let activeWorkout: ActiveWorkoutStore
    /// Finished workouts the server does not have yet, listed under the start section.
    let outbox: WorkoutOutbox
    let library: MovementLibraryStore
    let routines: RoutineStore
    let bodyweight: BodyweightStore
    /// The whole log and the pins, for the movement pages and the priorities this tab opens.
    let history: HistoryStore
    let focus: FocusStore
    /// Opens the logging surface. Held by the root so the bar and this tab open the same sheet
    /// rather than two.
    let openWorkout: () -> Void

    @State private var isPastWorkoutAsked = false
    /// Any value either destination below is registered for: this tab's own routes and the log's.
    @State private var route = NavigationPath()
    @State private var asked: AskedTransition?
    @State private var discarding: FinishedWorkout?
    /// The routine editor, open on a new routine or an existing one.
    @State private var editingRoutine: RoutineEditing?
    @State private var isSaveFromSessionAsked = false
    @State private var deletingRoutine: Routine?
    @State private var isNonTrainingAsked = false
    @Environment(TrainingBalanceStore.self) private var balance

    /// A transition waiting on the athlete's confirmation. Hung on the `List` rather than on a row,
    /// so there is one dialog rather than one per session.
    private struct AskedTransition: Identifiable {
        let transition: SessionTransition
        let session: TrainingSession.Planned

        var id: UUID { session.envelope.trainingSessionId }
    }

    /// Where this tab can navigate, as an enum rather than bare values because
    /// `navigationDestination` registers per type and a second screen must not silently win.
    enum Route: Hashable {
        case plannedSession(UUID)
        case bodyweight
    }

    var body: some View {
        NavigationStack(path: $route) {
            List {
                scoreSection
                startSection
                if !outbox.finished.isEmpty { finishedSection }
                NextSessionsSection()
                scorePartsSection
                expectationsSection
                volumeSection
                prioritiesSection
                routinesSection
                bodyweightSection
                switch store.state {
                case .loading:
                    loadingSections
                case .unavailable(let failure):
                    unavailableSection(failure)
                case .loaded(let plan):
                    planSections(store.withoutHeld(plan), staleBecause: nil)
                case .stale(let plan, let staleness):
                    planSections(store.withoutHeld(plan), staleBecause: staleness)
                }
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            .navigationTitle("Train")
            // The score leads the tab, so starting stays one tap away above it rather than a scroll
            // below it.
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Non-training days", systemImage: "calendar") { isNonTrainingAsked = true }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    if activeWorkout.workout != nil {
                        Button("Continue", action: openWorkout)
                    } else {
                        Button("Start") {
                            Task { if await store.startUnplanned() { openWorkout() } }
                        }
                        .disabled(store.action.isRunning)
                        .accessibilityLabel("Start workout")
                    }
                }
            }
            .refreshable {
                async let plan: Void = store.refresh()
                async let saved: Void = routines.refresh()
                async let weighed: Void = bodyweight.refresh()
                async let standing: Void = balance.refresh()
                _ = await (plan, saved, weighed, standing)
            }
            .navigationDestination(for: Route.self) { route in
                switch route {
                case .plannedSession(let id):
                    PlannedSessionView(
                        session: planned(id),
                        store: store,
                        library: library,
                        openWorkout: openWorkout)
                case .bodyweight:
                    BodyweightView(store: bodyweight)
                }
            }
            .navigationDestination(for: HistoryRoute.self) { route in
                HistoryDestination(route: route, history: history, focus: focus, library: library)
            }
            .navigationDestination(for: ScoreRoute.self) { route in
                ScoreDestination(route: route) { self.route.append(ScoreRoute.part($0)) }
            }
        }
        .task {
            async let plan: Void = store.refresh()
            async let saved: Void = routines.refresh()
            async let weighed: Void = bodyweight.refreshIfNeeded()
            // Read every time the tab is shown: a workout finished elsewhere moves all of it.
            async let standing: Void = balance.refresh()
            async let pins: Void = focus.refreshIfNeeded()
            _ = await (plan, saved, weighed, standing, pins)
            await library.refreshIfNeeded()
        }
        .sheet(item: $editingRoutine) { editing in
            RoutineEditorView(editing: editing, routines: routines, library: library)
        }
        .sheet(isPresented: $isSaveFromSessionAsked) {
            SaveSessionAsRoutineSheet(
                sessions: store.plan?.completed ?? [], routines: routines, library: library)
        }
        .confirmationDialog(
            "Delete this routine?",
            isPresented: Binding(get: { deletingRoutine != nil }, set: { if !$0 { deletingRoutine = nil } }),
            titleVisibility: .visible,
            presenting: deletingRoutine
        ) { routine in
            Button("Delete \(routine.name)", role: .destructive) {
                Task { await routines.delete(routine.routineId) }
                deletingRoutine = nil
            }
        } message: { _ in
            Text("Sessions trained from it keep their sets. Comparisons with it stop.")
        }
        .sheet(isPresented: $isNonTrainingAsked) {
            NonTrainingDaysSheet()
        }
        .sheet(isPresented: $isPastWorkoutAsked) {
            LogPastWorkoutSheet(store: store, openWorkout: openWorkout)
        }
        .confirmationDialog(
            asked?.transition.confirmation?.question ?? "",
            isPresented: Binding(get: { asked != nil }, set: { if !$0 { asked = nil } }),
            titleVisibility: .visible,
            presenting: asked
        ) { asked in
            if let confirmation = asked.transition.confirmation {
                Button(confirmation.proceed, role: .destructive) {
                    carryOut(asked.transition, on: asked.session)
                }
            }
        } message: { asked in
            // Named, because the dialog is hung on the list and points at its middle rather than
            // at the row that was swiped.
            if let confirmation = asked.transition.confirmation {
                Text("\(heading(of: asked.session).title) · \(confirmation.warning)")
            }
        }
        // Every one of these transitions can be refused — the session was started or skipped on the
        // web app, or deleted from under this window — and the list looks the same either way.
        .alert(
            "Refused",
            isPresented: Binding(
                get: { store.action.refusal != nil },
                set: { if !$0 { store.dismissRefusal() } })
        ) {
            Button("OK") { store.dismissRefusal() }
        } message: {
            if let refusal = store.action.refusal { Text(refusal.message) }
        }
        .confirmationDialog(
            "Discard this finished workout?",
            isPresented: Binding(get: { discarding != nil }, set: { if !$0 { discarding = nil } }),
            titleVisibility: .visible,
            presenting: discarding
        ) { finished in
            Button("Discard workout", role: .destructive) {
                outbox.discard(finished.id)
                discarding = nil
            }
        } message: { _ in
            Text("The server would not take it, and this phone holds the only copy. This cannot be undone.")
        }
    }

    // MARK: - What it is time for

    /// Push, pull and legs this week against their targets, opening the patterns and muscle groups.
    @ViewBuilder
    private var volumeSection: some View {
        switch balance.volume {
        case .loading:
            Section("Last 7 days") { SkeletonRows(count: 3) }
        case .unavailable(let failure):
            Section("Last 7 days") {
                Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
            }
        case .loaded(let volume), .stale(let volume, _):
            Section {
                ForEach(volume.families) { family in
                    VolumeRow(
                        label: family.family.name, sets: Double(family.hardSets), target: family.target,
                        lastTrained: family.lastTrained)
                }
                NavigationLink(value: ScoreRoute.volume) {
                    LabeledContent("Patterns and muscle groups") {
                        if !volume.unplaced.isEmpty {
                            Text("\(volume.unplaced.count) not counted").monospacedDigit()
                        }
                    }
                }
            } header: {
                Text("Last 7 days")
            } footer: {
                Text("Hard sets — working, drop and failure sets, never warm-ups — against this week's expectation for each family.")
            }
        }
    }

    /// The tab's hero: the week's score and what is next. Its shape while the first read is out, so
    /// the start section below does not jump when it lands.
    @ViewBuilder
    private var scoreSection: some View {
        switch balance.score {
        case .loading:
            HeroSection { SkeletonRows(count: 3) }
        case .unavailable(let failure):
            Section("Training score") {
                Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
            }
        case .loaded(let score), .stale(let score, _):
            TrainingScoreHero(score: score, weeks: balance.weeks.value ?? [], next: balance.nextSessions.value) {
                route.append(ScoreRoute.part($0))
            }
        }
    }

    /// The score's parts in words, under the start section so starting stays near the top.
    @ViewBuilder
    private var scorePartsSection: some View {
        if let score = balance.score.value {
            TrainingScorePartsSection(score: score)
        }
    }

    /// The week's expectations under the score's parts: the adjusted score, each target with its
    /// reasons, and the lift aims.
    @ViewBuilder
    private var expectationsSection: some View {
        if let score = balance.score.value {
            WeekExpectationsSection(score: score) { isNonTrainingAsked = true }
        }
    }

    /// The priorities, a line opening their screen.
    private var prioritiesSection: some View {
        Section {
            NavigationLink(value: HistoryRoute.focus) {
                LabeledContent("Priorities") {
                    Text(prioritySummary).lineLimit(1)
                }
            }
        }
    }

    /// "Dip, pull-up, hack squat", or how many families have none.
    private var prioritySummary: String {
        guard let report = balance.priorities.value else { return "" }
        let pinned = report.families.compactMap { family -> String? in
            guard case .chosen(let chosen) = family.lift, case .pinned = chosen.choice else { return nil }
            return chosen.name
        }
        return pinned.isEmpty ? "None pinned" : pinned.joined(separator: ", ")
    }

    // MARK: - Routines

    /// The saved sessions, each one tap from a workout. A long press edits or deletes; the two ways
    /// to make one sit under the list, the past session first because it is the faster.
    @ViewBuilder
    private var routinesSection: some View {
        Section {
            switch routines.state {
            case .loading:
                SkeletonRows(count: 2)
            case .unavailable(let failure):
                Text(failure.message)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            case .loaded(let list), .stale(let list, _):
                ForEach(list) { routineRow($0) }
            }
            Menu {
                Button("From a past session") { isSaveFromSessionAsked = true }
                Button("Empty routine") { editingRoutine = .new }
            } label: {
                Text("New routine")
            }
        } header: {
            Text("Routines")
        } footer: {
            if let refusal = routines.action.refusal {
                Text(refusal.message).foregroundStyle(.orange)
            } else if case .stale(_, let staleness) = routines.state, case .unreachable(let asOf) = staleness {
                Text("\(Formatting.asOf(asOf)). A routine starts without the server.")
            }
        }
    }

    private func routineRow(_ routine: Routine) -> some View {
        Button {
            Task { if await store.startUnplanned(from: routine) { openWorkout() } }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(routine.name)
                    .font(.headline)
                    .foregroundStyle(Color(.label))
                Text(routineDetail(routine))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(2)
            }
            .padding(.vertical, 2)
        }
        .disabled(store.isWorkoutInFlight || store.action.isRunning)
        .accessibilityHint("Starts a workout from this routine")
        .contextMenu {
            Button {
                Task { if await store.startUnplanned(from: routine) { openWorkout() } }
            } label: {
                Label("Start", systemImage: "play")
            }
            .disabled(store.isWorkoutInFlight)
            Button {
                editingRoutine = .existing(routine)
            } label: {
                Label("Edit", systemImage: "square.and.pencil")
            }
            Button(role: .destructive) {
                deletingRoutine = routine
            } label: {
                Label("Delete", systemImage: "trash")
            }
        }
        .swipeActions(edge: .trailing) {
            Button(role: .destructive) { deletingRoutine = routine } label: { Label("Delete", systemImage: "trash") }
            Button { editingRoutine = .existing(routine) } label: { Label("Edit", systemImage: "square.and.pencil") }
        }
    }

    /// "Dip, Bench press, Row… · 12 sets · last Tue 28 Oct".
    private func routineDetail(_ routine: Routine) -> String {
        let names = routine.movements.prefix(3).map { library.fullName(of: $0.movementDataId) }
        let more = routine.movements.count > 3 ? " +\(routine.movements.count - 3)" : ""
        var parts = ["\(names.joined(separator: ", "))\(more)", Formatting.count(routine.workingSetCount, "set")]
        if let last = routine.lastSession {
            parts.append("last \(Formatting.dayInline(last.envelope.date))")
        }
        return parts.joined(separator: " · ")
    }

    // MARK: - Bodyweight

    private var bodyweightSection: some View {
        Section {
            NavigationLink(value: Route.bodyweight) {
                LabeledContent("Bodyweight") {
                    if let latest = bodyweight.series.latest {
                        Text("\(Formatting.weight(latest.weight, latest.unit)) · \(Formatting.dayInline(latest.day))")
                            .monospacedDigit()
                    } else {
                        Text("Not logged")
                    }
                }
            }
        }
    }

    // MARK: - Finished, not yet delivered

    /// Workouts finished with no connection, which the server has not taken yet. They are sent
    /// whenever it answers; one it refused waits here, with its reason, for the athlete to discard.
    private var finishedSection: some View {
        Section {
            ForEach(outbox.finished) { finished in
                finishedRow(finished)
                    .swipeActions(edge: .trailing) {
                        if !finished.isWorthDelivering {
                            Button("Discard", role: .destructive) { discarding = finished }
                        }
                    }
            }
            if outbox.hasDeliverableWork {
                Button("Send now") { Task { await outbox.deliver() } }
            }
        } header: {
            Text("Not yet on the server")
        }
    }

    private func finishedRow(_ finished: FinishedWorkout) -> some View {
        let workout = finished.workout
        let length = Formatting.duration(seconds: max(0, finished.endedAt.timeIntervalSince(workout.startedAt)))
        let ended = finished.endedAt.formatted(.dateTime.weekday(.abbreviated).hour().minute())

        return VStack(alignment: .leading, spacing: 2) {
            Text("\(Formatting.count(workout.sets.count, "set")) · \(length)")
                .font(.subheadline.weight(.semibold))
                .monospacedDigit()
            switch finished.delivery {
            case .waiting:
                Text("Ended \(ended) · waiting to send")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            case .refused(let reason):
                Text("Not accepted: \(reason)")
                    .font(.caption)
                    .foregroundStyle(.orange)
            }
        }
    }

    // MARK: - Acting on a planned session

    /// Runs a transition, or asks first when it cannot be undone.
    private func ask(_ transition: SessionTransition, about session: TrainingSession.Planned) {
        guard transition.confirmation != nil else {
            carryOut(transition, on: session)
            return
        }
        asked = AskedTransition(transition: transition, session: session)
    }

    private func carryOut(_ transition: SessionTransition, on session: TrainingSession.Planned) {
        asked = nil
        Task {
            switch transition {
            case .start:
                if await store.start(planned: session) { openWorkout() }
            case .skip:
                await store.skip(session)
            case .delete:
                await store.delete(session)
            }
        }
    }

    // MARK: - Starting

    @ViewBuilder
    private var startSection: some View {
        Section {
            if activeWorkout.workout != nil {
                Button(action: openWorkout) {
                    Text("Continue workout")
                        .fontWeight(.semibold)
                }
            } else {
                if let abandoned = store.plan?.inProgress {
                    resumeRow(abandoned)
                }
                Button {
                    Task {
                        if await store.startUnplanned() { openWorkout() }
                    }
                } label: {
                    Text("Start workout")
                        .fontWeight(.semibold)
                }
                .disabled(store.action.isRunning)

                Button {
                    isPastWorkoutAsked = true
                } label: {
                    Text("Log a past workout")
                }
            }
        } header: {
            Text("Now")
        } footer: {
            if activeWorkout.workout == nil, store.plan?.inProgress != nil {
                Text("An unfinished session is open on the server. Resuming picks up its sets.")
            }
        }
    }

    /// A session the server says is active that this device is not holding — started on the web
    /// app, or left behind by a reinstall.
    private func resumeRow(_ session: TrainingSession.Active) -> some View {
        Button {
            if store.resume(session, routine: session.envelope.routineId.flatMap(routines.routine)) { openWorkout() }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text("Resume session in progress")
                    .fontWeight(.semibold)
                Text("Started \(session.startedAt.formatted(date: .abbreviated, time: .shortened))"
                     + " · \(Formatting.count(session.sets.count, "set"))")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    // MARK: - The plan

    @ViewBuilder
    private func planSections(_ plan: TrainingPlan, staleBecause staleness: Staleness?) -> some View {
        if let staleness {
            StaleBanner(staleness)
        }

        if !plan.today.isEmpty {
            Section("Today") {
                ForEach(plan.today, id: \.envelope.trainingSessionId) { plannedRow($0) }
            }
        }
        if !plan.upcoming.isEmpty {
            Section("Coming up") {
                ForEach(plan.upcoming, id: \.envelope.trainingSessionId) { plannedRow($0) }
            }
        }
        if !plan.missed.isEmpty {
            Section {
                ForEach(plan.missed, id: \.envelope.trainingSessionId) { plannedRow($0) }
            } header: {
                Text("Missed")
            } footer: {
                Text("Planned for a past day. A late start is recorded on the day it happens.")
            }
        }
        if plan.hasNothingPlanned {
            Section {
                Text("Nothing planned. Sessions are planned on the web app.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    /// What a planned session is called, and its day.
    ///
    /// What the athlete called it leads, where they called it anything, and the day goes in the
    /// line below. The day cannot lead: these rows sit under headers that are themselves days, so a
    /// session planned for today would read "Today" under "Today" — the same word twice, saying
    /// nothing either time.
    private func heading(of session: TrainingSession.Planned) -> (title: String, day: String) {
        let summary = PlanSummary.of(session)
        let day = Formatting.day(RelativeDay.of(summary.plannedFor, today: CalendarDay(Date())))
        return (summary.hasNotes ? summary.notes : day, day)
    }

    private func plannedRow(_ session: TrainingSession.Planned) -> some View {
        let summary = PlanSummary.of(session)
        let heading = heading(of: session)

        return NavigationLink(value: Route.plannedSession(session.envelope.trainingSessionId)) {
            VStack(alignment: .leading, spacing: 4) {
                Text(heading.title)
                    .font(.headline)
                    .lineLimit(2)
                Text(detail(of: summary, on: heading.day))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            .padding(.vertical, 2)
        }
        .contextMenu { menu(for: session) }
        // Starting leads from the leading edge and skipping from the trailing one, which is where
        // iOS puts the constructive and the destructive gesture everywhere else.
        .swipeActions(edge: .leading) {
            Button {
                ask(.start, about: session)
            } label: {
                Label("Start", systemImage: "play.fill")
            }
            .tint(.green)
            .disabled(store.isWorkoutInFlight)
        }
        .swipeActions(edge: .trailing) {
            Button(role: .destructive) {
                ask(.skip, about: session)
            } label: {
                Label("Skip", systemImage: "xmark")
            }
        }
    }

    /// The long press: every transition this session's state allows, and nothing it does not.
    ///
    /// The state is read through `SessionLog.kind` so that a session planned for a day gone by is
    /// treated as the log treats it — missed, and still startable, because training it late is
    /// normal.
    @ViewBuilder
    private func menu(for session: TrainingSession.Planned) -> some View {
        let kind = SessionLog.kind(of: .planned(session), today: CalendarDay(Date()))

        ForEach(SessionTransition.offered(on: kind)) { transition in
            Button(role: transition.isDestructive ? .destructive : nil) {
                ask(transition, about: session)
            } label: {
                Label(transition.label, systemImage: transition.systemImage)
            }
            // A second workout would give the device two to log into and a way to reach only one.
            .disabled(transition == .start && store.isWorkoutInFlight)
        }
    }

    private func detail(of summary: PlanSummary, on day: String) -> String {
        var parts = [day]
        if summary.prescriptionCount > 0 {
            parts.append(Formatting.count(summary.prescriptionCount, "set"))
            if summary.movementCount > 0 {
                parts.append(Formatting.count(summary.movementCount, "movement"))
            }
        } else {
            parts.append("nothing prescribed")
        }
        return parts.joined(separator: " · ")
    }

    /// The planned session behind a route value, read back from the loaded plan.
    ///
    /// The route carries the id rather than the session so that a refresh while the detail screen
    /// is open shows the refreshed session instead of a copy taken when it was pushed.
    private func planned(_ id: UUID) -> TrainingSession.Planned? {
        guard let plan = store.plan else { return nil }
        return (plan.today + plan.upcoming + plan.missed)
            .first { $0.envelope.trainingSessionId == id }
    }

    // MARK: - Not loaded

    /// The plan's own shape, in grey. The start section above it is drawn and usable already —
    /// logging a workout you already did needs no server at all — so only the plan waits.
    private var loadingSections: some View {
        Section("Coming up") { SkeletonRows(count: 3) }
    }

    private func unavailableSection(_ failure: LoadFailure) -> some View {
        Section {
            VStack(alignment: .leading, spacing: 6) {
                Label("Your plan is not available", systemImage: "exclamationmark.triangle")
                    .font(.subheadline.weight(.semibold))
                Text(failure.message)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                if failure.isWorthRetrying {
                    Button("Try again") { Task { await store.refresh() } }
                        .font(.footnote)
                }
            }
        } footer: {
            // No workout needs the server to begin, so the start section above stays useful here.
            Text("A workout can still be started or logged. It is held on this phone until the server answers.")
        }
    }
}
