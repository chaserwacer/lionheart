import SwiftUI

/// Let me do it.
///
/// The tab answers one question — what am I training, and how do I start — so the first section is
/// always the answer to it: continue what is running, resume what the server has, or start one of
/// the three ways. Everything below that is the plan.
struct TrainView: View {
    let store: TrainStore
    let activeWorkout: ActiveWorkoutStore
    let library: MovementLibraryStore
    /// Opens the logging surface. Held by the root so the bar and this tab open the same cover
    /// rather than two.
    let openWorkout: () -> Void

    @State private var isPastWorkoutAsked = false
    @State private var route: [Route] = []
    @State private var asked: AskedTransition?

    /// A transition waiting on the athlete's confirmation. Hung on the `List` rather than on a row,
    /// so there is one dialog rather than one per session.
    private struct AskedTransition: Identifiable {
        let transition: SessionTransition
        let session: TrainingSession.Planned

        var id: UUID { session.envelope.trainingSessionId }
    }

    /// Where this tab can navigate. One case today, and an enum rather than a bare value because
    /// `navigationDestination` registers per type and a second screen must not silently win.
    enum Route: Hashable {
        case plannedSession(UUID)
    }

    var body: some View {
        NavigationStack(path: $route) {
            List {
                startSection
                switch store.state {
                case .loading:
                    loadingSections
                case .unavailable(let failure):
                    unavailableSection(failure)
                case .loaded(let plan):
                    planSections(plan, staleBecause: nil)
                case .stale(let plan, let failure):
                    planSections(plan, staleBecause: failure)
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Train")
            .refreshable { await store.refresh() }
            .navigationDestination(for: Route.self) { route in
                switch route {
                case .plannedSession(let id):
                    PlannedSessionView(
                        session: planned(id),
                        store: store,
                        library: library,
                        openWorkout: openWorkout)
                }
            }
        }
        .task {
            await store.refresh()
            await library.refreshIfNeeded()
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
            if let confirmation = asked.transition.confirmation { Text(confirmation.warning) }
        }
        // Every one of these transitions can be refused — the session was started or skipped on the
        // web app, or deleted from under this window — and the list looks the same either way.
        .alert(
            "That did not happen",
            isPresented: Binding(
                get: { store.action.refusal != nil },
                set: { if !$0 { store.dismissRefusal() } })
        ) {
            Button("OK") { store.dismissRefusal() }
        } message: {
            if let refusal = store.action.refusal { Text(refusal.message) }
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
                    Label("Continue your workout", systemImage: "figure.strengthtraining.traditional")
                        .fontWeight(.semibold)
                }
            } else {
                if let abandoned = store.state.value?.inProgress {
                    resumeRow(abandoned)
                }
                Button {
                    Task {
                        if await store.startUnplanned() { openWorkout() }
                    }
                } label: {
                    Label("Start a workout", systemImage: "play.circle")
                        .fontWeight(.semibold)
                }
                .disabled(store.action.isRunning)

                Button {
                    isPastWorkoutAsked = true
                } label: {
                    Label("Log a workout you already did", systemImage: "square.and.pencil")
                }
            }
        } header: {
            Text("Now")
        } footer: {
            if activeWorkout.workout == nil, store.state.value?.inProgress != nil {
                Text("There is an unfinished session on the server. Resuming picks up its sets.")
            }
        }
    }

    /// A session the server says is active that this device is not holding — started on the web
    /// app, or left behind by a reinstall.
    private func resumeRow(_ session: TrainingSession.Active) -> some View {
        Button {
            if store.resume(session) { openWorkout() }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Label("Resume the session in progress", systemImage: "arrow.clockwise.circle")
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
    private func planSections(_ plan: TrainingPlan, staleBecause failure: LoadFailure?) -> some View {
        if let failure {
            StaleBanner(failure: failure)
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
                Text("Planned for a day gone by. Starting one late is fine — it is recorded on the day you do it.")
            }
        }
        if plan.hasNothingPlanned {
            Section {
                Text("Nothing planned. Sessions are planned on the Lionheart web app, or just start one above.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    private func plannedRow(_ session: TrainingSession.Planned) -> some View {
        let summary = PlanSummary.of(session)
        let day = Formatting.day(RelativeDay.of(summary.plannedFor, today: CalendarDay(Date())))

        return NavigationLink(value: Route.plannedSession(session.envelope.trainingSessionId)) {
            VStack(alignment: .leading, spacing: 4) {
                // What the athlete called it leads, where they called it anything, and the day goes
                // in the line below. The day cannot lead: these rows sit under headers that are
                // themselves days, so a session planned for today would read "Today" under
                // "Today" — the same word twice, saying nothing either time.
                Text(summary.hasNotes ? summary.notes : day)
                    .font(.headline)
                    .lineLimit(2)
                Text(detail(of: summary, on: day))
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
        guard let plan = store.state.value else { return nil }
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
            // Starting a workout needs the server, but logging one you already did does not — so
            // the start section above stays useful even here.
            Text("You can still log a workout you already did; it is saved on this phone until you finish it.")
        }
    }
}
