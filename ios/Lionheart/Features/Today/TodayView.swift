import SwiftUI

/// How am I, and what did I do — today, or any day before it.
///
/// Read top to bottom the way a morning is: the day's verdict first, large; the figures behind it
/// as a table, each against the athlete's own four-week baseline; then the training. Every
/// decision on it — which day's recovery to show, which session is the last one, what counts as
/// unusual — is a pure function in `Core/Derived/`; this file chooses words and containers.
///
/// The day steps back and forward from the toolbar, the way Health and Oura step through days. A
/// past day is read for what happened on it; the questions about now — the next session, the one
/// in progress, the plain-words log — belong to today only.
struct TodayView: View {
    let store: TodayStore
    let ouraSync: OuraSyncStore
    let healthSync: HealthSyncStore
    /// The shown day's movement timeline, read beside the day's other reads.
    let habits: HabitsStore
    /// Names the movements in each session's lines.
    let library: MovementLibraryStore
    /// The whole log, which a movement's page opened from a training line reads.
    let history: HistoryStore
    let focus: FocusStore
    /// Passed through to Settings, which states it. Carried rather than re-read from the bundle
    /// so there is one place configuration is resolved.
    let apiBaseURL: URL
    /// Passed through to Settings, whose sign-out says what it would cost the workout in flight.
    let activeWorkout: ActiveWorkoutStore

    @State private var isQuickLogPresented = false
    @State private var isSettingsPresented = false
    @State private var isDayPickerPresented = false
    @State private var isRecoveryExplained = false

    /// Where the Today tab can navigate, registered once on the stack.
    enum Route: Hashable {
        case session(UUID)
        case trend(RecoveryMetric, endingOn: CalendarDay)
        /// The night behind the recovery figures, by its waking day.
        case night(CalendarDay)
        case dayActivity(CalendarDay)
    }

    var body: some View {
        let today = store.today
        let day = store.selection.day(today: today)

        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    loadingRows(isToday: store.selection.isToday)
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let snapshot):
                    content(snapshot, today: today, staleBecause: nil)
                case .stale(let snapshot, let staleness):
                    content(snapshot, today: today, staleBecause: staleness)
                }
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            // Lets a figure row be as short as its one line, so the whole table fits above the fold.
            .environment(\.defaultMinListRowHeight, 36)
            .recalledNote(store.state.recalledAsOf)
            .navigationTitle(title(of: day, today: today))
            .toolbar { toolbar(day: day, today: today) }
            .refreshable {
                async let today: Void = store.refresh()
                async let movement: Void = habits.loadDay(day)
                _ = await (today, movement)
            }
            .navigationDestination(for: Route.self) { destination(for: $0) }
            .navigationDestination(for: HistoryRoute.self) { route in
                HistoryDestination(route: route, history: history, focus: focus, library: library)
            }
            .sheet(isPresented: $isQuickLogPresented) {
                QuickLogSheet(store: store)
            }
            .sheet(isPresented: $isSettingsPresented) {
                SettingsSheet(apiBaseURL: apiBaseURL, activeWorkout: activeWorkout, healthSync: healthSync)
            }
            .sheet(isPresented: $isRecoveryExplained) {
                RecoveryExplanationSheet()
            }
            .sheet(isPresented: $isDayPickerPresented) {
                DayPickerSheet(day: day, today: today) { chosen in
                    Task { await store.show(DaySelection.of(chosen, today: today)) }
                }
            }
        }
        // Every return to the tab, but a read only when the day may have moved (`ReadFreshness`).
        .task { await store.refreshIfDue() }
        .task(id: day) { await habits.loadDayIfNeeded(day) }
    }

    // MARK: - Toolbar

    @ToolbarContentBuilder
    private func toolbar(day: CalendarDay, today: CalendarDay) -> some ToolbarContent {
        ToolbarItemGroup(placement: .topBarLeading) {
            Button("Previous day", systemImage: "chevron.left") {
                step(by: -1, today: today)
            }
            Button("Next day", systemImage: "chevron.right") {
                step(by: 1, today: today)
            }
            .disabled(store.selection.isToday)
        }
        ToolbarItemGroup(placement: .topBarTrailing) {
            Button("Choose day", systemImage: "calendar") { isDayPickerPresented = true }
            Button("Settings", systemImage: "gearshape") { isSettingsPresented = true }
        }
    }

    private func step(by days: Int, today: CalendarDay) {
        Task { await store.show(store.selection.stepped(by: days, today: today)) }
    }

    private func title(of day: CalendarDay, today: CalendarDay) -> String {
        switch RelativeDay.of(day, today: today) {
        case .today: "Today"
        case .yesterday: "Yesterday"
        case .tomorrow, .daysAgo, .inDays, .distant: Formatting.date(day)
        }
    }

    @ViewBuilder
    private func destination(for route: Route) -> some View {
        switch route {
        case .session(let id):
            if let session = store.state.value.flatMap({ session(id, in: $0) }) {
                SessionDetailView(session: session, catalogue: catalogue, history: history, library: library)
            } else {
                ContentUnavailableView {
                    Text("Session not available")
                } description: {
                    Text("It is no longer on the day Today has loaded.")
                }
            }
        case .trend(let metric, let endingOn):
            MetricTrendView(
                metric: metric,
                endingOn: endingOn,
                trend: { store.trends[$0] },
                load: { await store.loadTrend($0) })
        case .night(let day):
            if let night = store.state.value?.night, night.day == day {
                NightView(night: night)
            } else {
                ContentUnavailableView {
                    Text("Night not available")
                } description: {
                    Text("It is no longer on the day Today has loaded.")
                }
            }
        case .dayActivity(let day):
            DayActivityView(day: day, store: habits)
        }
    }

    private func session(_ id: UUID, in snapshot: TodaySnapshot) -> TrainingSession? {
        snapshot.sessionsOnDay.first { $0.id == id }
            ?? snapshot.lastCompleted.map(TrainingSession.completed).flatMap { $0.id == id ? $0 : nil }
    }

    private var catalogue: MovementCatalogue { library.catalogue }

    // MARK: - Loaded

    @ViewBuilder
    private func content(
        _ snapshot: TodaySnapshot, today: CalendarDay, staleBecause staleness: Staleness?
    ) -> some View {
        if let staleness {
            // The value stays; the banner says it could not be updated. Blanking a loaded screen
            // because a refresh failed is the thing LoadState exists to prevent.
            StaleBanner(staleness)
        }

        let isToday = snapshot.day == today
        recoverySection(snapshot, today: today)
        bodyDaySection(snapshot)
        // Under the figures it qualifies, so the verdict leads, and in a section of its own so the
        // figures' footer stays with them: how current they are, and the control that updates them.
        if isToday {
            Section {
                OuraSyncRow(store: ouraSync, today: today)
                HealthSyncRow(store: healthSync)
            }
        }
        if let active = snapshot.inProgress { inProgressSection(active) }
        trainingSection(snapshot, today: today)
        if isToday { TrainingScoreSection() }
        if isToday {
            nextSessionSection(snapshot.nextPlanned, today: today)
            quickLogSection
        }
    }

    /// The verdict, then the readiness contributors that answer "how recovered am I", each Oura's
    /// score beside the measurement it rests on, then the figures no contributor carries.
    @ViewBuilder
    private func recoverySection(_ snapshot: TodaySnapshot, today: CalendarDay) -> some View {
        let day = snapshot.day
        Section {
            if let recovery = snapshot.recovery {
                if let readiness = recovery.readiness {
                    NavigationLink(value: Route.trend(.readiness, endingOn: recovery.day)) {
                        RecoveryVerdictView(
                            readiness: readiness, figure: recovery.readinessFigure, shownDay: day, today: today)
                    }
                }
                ForEach(snapshot.contributors) { reading in
                    if let route = route(for: reading, on: recovery.day) {
                        NavigationLink(value: route) { ReadinessContributorRow(reading: reading) }
                    } else {
                        ReadinessContributorRow(reading: reading)
                    }
                }
                ForEach(snapshot.otherFigures) { figure in
                    NavigationLink(value: Route.trend(figure.metric, endingOn: recovery.day)) {
                        RecoveryFigureRow(reading: figure)
                    }
                }
            } else {
                Text(day == today ? "No Oura data in the last two weeks." : "No Oura data for this day.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            HStack(alignment: .firstTextBaseline) {
                Text("Recovery")
                Spacer()
                if !snapshot.contributors.isEmpty {
                    Text("Oura")
                        .accessibilityHidden(true)
                }
                Button("How to read this", systemImage: "info.circle") { isRecoveryExplained = true }
                    .labelStyle(.iconOnly)
            }
        } footer: {
            if let recovery = snapshot.recovery, !recovery.figures.isEmpty {
                Text(footer(for: recovery, shownDay: day, today: today))
            }
        }
    }

    /// Where a contributor row opens: the measurement's own trend, or the night for the recovery
    /// index, whose measurement is a point on the night's curve.
    private func route(for reading: ReadinessContributorReading, on day: CalendarDay) -> Route? {
        switch reading.basis {
        case .measured(let figure): .trend(figure.metric, endingOn: day)
        case .lowestHeartRate: .night(day)
        case .recentSleep: .trend(.sleepDuration, endingOn: day)
        case nil: nil
        }
    }

    /// The night the figures came from and the shown day's movement, each a strip that opens onto
    /// its own screen.
    @ViewBuilder
    private func bodyDaySection(_ snapshot: TodaySnapshot) -> some View {
        // A day stored before the five-minute classification was kept has no shape to draw; its
        // steps are already among the figures above.
        let movement = habits.days[snapshot.day]?.value.flatMap(\.self).flatMap { $0.runs.isEmpty ? nil : $0 }
        if snapshot.night != nil || movement != nil {
            Section {
                if let night = snapshot.night {
                    NavigationLink(value: Route.night(night.day)) {
                        NightSummaryRow(
                            night: night,
                            caption: night.day == snapshot.day
                                ? nil
                                : "Night of \(Formatting.date(night.day.adding(days: -1)))")
                    }
                }
                if let movement {
                    NavigationLink(value: Route.dayActivity(snapshot.day)) {
                        DayActivityRow(detail: movement)
                    }
                }
            } header: {
                Text("Night and day")
            }
        }
    }

    private func footer(for recovery: RecoveryReading, shownDay: CalendarDay, today: CalendarDay) -> String {
        let against = "Tracks are Oura's scores out of 100; figures are measured, against your \(MetricBaseline.windowDays) days before."
        guard recovery.readiness == nil, recovery.day != shownDay else { return against }
        return "From \(Formatting.day(RelativeDay.of(recovery.day, today: today)).lowercased()). \(against)"
    }

    @ViewBuilder
    private func inProgressSection(_ session: TrainingSession.Active) -> some View {
        Section("In progress") {
            LabeledContent("Started") {
                Text(session.startedAt, style: .time)
            }
            LabeledContent("Sets logged") {
                Text("\(session.sets.count)").monospacedDigit()
            }
        }
    }

    /// Each session as a head row that opens it, and each of its movements as a row of its own
    /// that opens the movement's page — its history, charts and records.
    ///
    /// The movements the sessions name are handed to the catalogue to cover, keyed on the set of
    /// them, so a movement made since the catalogue loaded is read for once rather than shown as
    /// "Unknown movement".
    @ViewBuilder
    private func trainingSection(_ snapshot: TodaySnapshot, today: CalendarDay) -> some View {
        let shown = snapshot.sessionsOnDay.map { ($0, nil as String?) }
            + (snapshot.lastCompleted.map {
                let session = TrainingSession.completed($0)
                return [(session, "Last session · \(Formatting.day(RelativeDay.of(session.date, today: today)))")]
            } ?? [])
        let referenced = shown.reduce(into: Set<UUID>()) { $0.formUnion($1.0.referencedMovementIds) }

        Section("Training") {
            ForEach(shown, id: \.0.id) { session, dayLabel in
                NavigationLink(value: Route.session(session.id)) {
                    TrainingDayRow(session: session, dayLabel: dayLabel)
                }
                ForEach(MovementDigest.of(session.sets, catalogue: catalogue)) { digest in
                    if let movementDataId = digest.movementDataId {
                        NavigationLink(value: HistoryRoute.movement(movementDataId)) {
                            MovementLineRow(digest: digest)
                        }
                    } else {
                        MovementLineRow(digest: digest)
                    }
                }
            }
            if snapshot.sessionsOnDay.isEmpty, snapshot.lastCompleted == nil, snapshot.inProgress == nil {
                Text(snapshot.day == today ? "Nothing logged." : "Nothing logged on this day.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .task(id: referenced) { await library.cover(referenced) }
    }

    @ViewBuilder
    private func nextSessionSection(_ session: TrainingSession.Planned?, today: CalendarDay) -> some View {
        if let session {
            Section("Next session") {
                let summary = PlanSummary.of(session)
                VStack(alignment: .leading, spacing: 4) {
                    Text(Formatting.day(RelativeDay.of(summary.plannedFor, today: today)))
                        .font(.subheadline.weight(.semibold))
                    Text(detail(of: summary))
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                    if summary.hasNotes {
                        Text(summary.notes)
                            .font(.footnote)
                            .foregroundStyle(Color(.secondaryLabel))
                            .lineLimit(3)
                    }
                }
                .padding(.vertical, 2)
            }
        }
    }

    private var quickLogSection: some View {
        Section {
            Button("Log in plain words") { isQuickLogPresented = true }
        } footer: {
            Text("Free text, parsed into structured entries.")
        }
    }

    private func detail(of summary: PlanSummary) -> String {
        guard summary.prescriptionCount > 0 else { return "No prescriptions yet" }
        var parts = [Formatting.count(summary.prescriptionCount, "prescription")]
        if summary.movementCount > 0 {
            parts.append(Formatting.count(summary.movementCount, "movement"))
        }
        return parts.joined(separator: " · ")
    }

    // MARK: - Not loaded

    /// The screen's own shape while its requests are out, under the headers the content will
    /// arrive under — so the rows appear in place rather than pushing a spinner off the screen.
    @ViewBuilder
    private func loadingRows(isToday: Bool) -> some View {
        Section("Recovery") { SkeletonRows(count: 3) }
        if isToday {
            Section {
                OuraSyncRow(store: ouraSync, today: store.today)
                HealthSyncRow(store: healthSync)
            }
        }
        Section("Training") { SkeletonRows(count: 1) }
        if isToday { quickLogSection }
    }

    @ViewBuilder
    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Today is not available", systemImage: "exclamationmark.triangle")
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

/// A month calendar for jumping to a day, with the future closed off.
private struct DayPickerSheet: View {
    /// The day on screen, which the calendar opens on.
    let day: CalendarDay
    let today: CalendarDay
    let choose: (CalendarDay) -> Void

    @Environment(\.dismiss) private var dismiss
    /// The date picked, or `nil` until one is.
    @State private var picked: Date?

    private var date: Date { picked ?? day.middayDate() }

    var body: some View {
        NavigationStack {
            DatePicker(
                "Day",
                selection: Binding(get: { date }, set: { picked = $0 }),
                in: ...today.middayDate(),
                displayedComponents: .date)
                .datePickerStyle(.graphical)
                .padding(.horizontal)
                .navigationTitle("Go to day")
                .navigationBarTitleDisplayMode(.inline)
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("Cancel") { dismiss() }
                    }
                    ToolbarItem(placement: .confirmationAction) {
                        Button("Go") {
                            choose(CalendarDay(date))
                            dismiss()
                        }
                    }
                    ToolbarItem(placement: .bottomBar) {
                        Button("Today") {
                            choose(today)
                            dismiss()
                        }
                    }
                }
        }
        .presentationDetents([.medium, .large])
    }
}
