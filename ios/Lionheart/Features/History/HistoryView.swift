import SwiftUI

/// What have I done?
///
/// The training log is the screen, because that is the question, read as a diary: weeks newest
/// first, each headed by its sessions and opening onto the week's lifting, cardio and body; each row
/// a day, a log-book entry of what was lifted, opening onto everything else the day held. A run of
/// weeks with nothing logged is said once, between the weeks either side of it. Above it, the three readings of the log that answer the athlete's goals:
/// what he has pinned to emphasise, how consistently he trains, and the ways into a movement,
/// the records and the trends. They are destinations above the log rather than a segmented control
/// fighting the search field for the top of the screen, and they step aside while searching.
///
/// The log is filtered by movement with search tokens: typing suggests movements, and choosing one
/// keeps the sessions that performed it — by identity, so a paused squat does not bring in the
/// high-bar ones. Free text still matches names and notes.
struct HistoryView: View {
    let store: HistoryStore
    let focus: FocusStore
    let library: MovementLibraryStore

    @State private var query = ""
    @State private var tokens: [MovementToken] = []
    @State private var asked: AskedTransition?
    /// The stack's path, held so a context menu can push a movement's page.
    @State private var path = NavigationPath()
    @ScaledMetric(relativeTo: .body) private var consistencyChartHeight: CGFloat = 64
    /// Pulling to refresh brings in any new runs from Apple Health with the log.
    @Environment(HealthSyncStore.self) private var healthSync

    /// A transition waiting on the athlete's confirmation.
    ///
    /// Held by the screen rather than the row, because the dialog is hung on the `List`: a
    /// `confirmationDialog` on each row is a dialog per session, and the one that opens is then
    /// whichever row SwiftUI happened to keep.
    private struct AskedTransition: Identifiable {
        let transition: SessionTransition
        let session: LoggedSession

        var id: UUID { session.sessionId }
    }

    var body: some View {
        NavigationStack(path: $path) {
            List {
                switch store.state {
                case .loading:
                    Section("Sessions") { SkeletonRows(count: 6) }
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let snapshot):
                    content(snapshot, staleBecause: nil)
                case .stale(let snapshot, let staleness):
                    content(snapshot, staleBecause: staleness)
                }
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            .recalledNote(store.state.recalledAsOf)
            .navigationTitle("History")
            .searchable(text: $query, tokens: $tokens, prompt: "Movement or note") { token in
                Text(token.name)
            }
            // Movements as completions rather than as `suggestedTokens`, which iOS 26 does not show
            // while the field is minimized into the toolbar.
            .searchSuggestions {
                ForEach(suggestedTokens) { token in
                    Label(token.name, systemImage: "line.3.horizontal.decrease")
                        .searchCompletion(token)
                }
            }
            .minimizingSearchField()
            .refreshable {
                async let runs: Void = healthSync.syncIfConnected()
                async let log: Void = refresh()
                _ = await (runs, log)
            }
            .navigationDestination(for: HistoryRoute.self) { route in
                HistoryDestination(route: route, history: store, focus: focus, library: library)
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
                if let confirmation = asked.transition.confirmation {
                    Text("\(asked.session.headline) · \(confirmation.warning)")
                }
            }
        }
        // A refusal is the one outcome a list cannot show: the row is still there whether the
        // server took the action or refused it. Hung on the stack rather than on the log, because
        // a record is retracted from a pushed screen and an alert from a view underneath the top
        // of the stack has nowhere to present.
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
        // Every return to the tab, but the log is read only when it may have moved
        // (`ReadFreshness`): it is the heaviest read in the app.
        .task {
            async let log: Void = store.refreshIfDue()
            async let pins: Void = focus.refreshIfNeeded()
            _ = await (log, pins)
        }
    }

    private func refresh() async {
        async let log: Void = store.refresh()
        async let pins: Void = focus.refresh()
        _ = await (log, pins)
    }

    /// Runs a transition, or asks first when it is one that cannot be undone.
    private func ask(_ transition: SessionTransition, about session: LoggedSession) {
        guard transition.confirmation != nil else {
            carryOut(transition, on: session)
            return
        }
        asked = AskedTransition(transition: transition, session: session)
    }

    private func carryOut(_ transition: SessionTransition, on session: LoggedSession) {
        asked = nil
        Task {
            switch transition {
            case .skip: await store.skip(session.sessionId)
            case .delete: await store.delete(session.sessionId)
            case .start:
                // Not offered here: starting a session hands this device a workout to log into, and
                // the screen that does the logging belongs to Train. `transitions(on:)` drops it.
                break
            }
        }
    }

    // MARK: - Search

    /// The movements the typed text names, offered as tokens: the ones trained most recently first,
    /// and only while there is text, so an empty field suggests nothing.
    private var suggestedTokens: [MovementToken] {
        guard query.nonEmpty != nil, let index = store.state.value?.movements else { return [] }
        let chosen = Set(tokens.map(\.id))
        return index.matching(query)
            .filter { !chosen.contains($0.movementDataId) }
            .prefix(8)
            .map { MovementToken(id: $0.movementDataId, name: $0.name) }
    }

    private var isSearching: Bool { query.nonEmpty != nil || !tokens.isEmpty }

    // MARK: - Loaded

    @ViewBuilder
    private func content(_ snapshot: HistorySnapshot, staleBecause staleness: Staleness?) -> some View {
        if let staleness {
            StaleBanner(staleness)
        }

        let log = snapshot.log
            .filtered(byMovements: Set(tokens.map(\.id)))
            .filtered(by: query)

        if isSearching {
            ForEach(tokens) { token in
                Section {
                    NavigationLink(value: HistoryRoute.movement(token.id)) {
                        LabeledContent(token.name) {
                            let own = snapshot.movements.entries.first { $0.movementDataId == token.id }
                            Text(Formatting.count(own?.sessionCount ?? 0, "session")).monospacedDigit()
                        }
                    }
                }
            }
            if log.isEmpty {
                searchFoundNothing
            } else {
                ForEach(log.weeks) { week in weekSection(week) }
            }
        } else {
            focusSection(snapshot)
            consistencySection(snapshot)
            readingsSection(snapshot)
            if snapshot.log.isEmpty {
                nothingLoggedYet
            } else {
                ForEach(log.stretches()) { stretch in
                    switch stretch {
                    case .week(let week): weekSection(week)
                    case .quiet(let first, let count): quietSection(from: first, weeks: count)
                    }
                }
            }
        }
    }

    /// The pins being worked on, each a line of how it is going, opening the movement it emphasises.
    /// Nothing at all when nothing is pinned — the focus list is still one row away below.
    @ViewBuilder
    private func focusSection(_ snapshot: HistorySnapshot) -> some View {
        let today = CalendarDay(Date())
        let current = (focus.state.value ?? [])
            .map { FocusReading.of($0, history: snapshot.history(of: $0.movementDataId, today: today), today: today) }
            .arrangedForReading().current

        if !current.isEmpty {
            Section {
                ForEach(current) { reading in
                    NavigationLink(value: HistoryRoute.movement(reading.pin.movementDataId)) {
                        FocusSummaryRow(reading: reading, name: snapshot.catalogue.name(for: reading.pin.movementDataId))
                    }
                }
            } header: {
                Text("Priorities")
            }
        }
    }

    /// Sessions a week over the last four months, with this week, the four-week average and the
    /// weeks that held a session. It opens the training calendar.
    @ViewBuilder
    private func consistencySection(_ snapshot: HistorySnapshot) -> some View {
        let window = snapshot.consistency.window(lastWeeks: Self.consistencyWeeksShown)

        if !window.weeks.isEmpty {
            Section {
                NavigationLink(value: HistoryRoute.consistency) {
                    VStack(alignment: .leading, spacing: 8) {
                        SummaryFigures(figures: [
                            ("This week", Formatting.count(window.weeks.last?.sessionCount ?? 0, "session")),
                            ("4-week average", Formatting.rate(window.average.last?.value ?? 0)),
                            ("Weeks trained", "\(window.weeksTrained) of \(window.weeks.count)"),
                        ])
                        if window.sessions > 0 {
                            WeeklyLoadChart(
                                weeks: window.weeks, average: window.average, counting: .sessions, showsAxes: false)
                                .frame(height: consistencyChartHeight)
                        }
                    }
                    .padding(.vertical, 4)
                }
            } header: {
                Text("Sessions per week")
            }
        }
    }

    /// Four months: long enough to see a block and a gap, short enough that a week is a bar.
    private static let consistencyWeeksShown = 16

    /// The ways into the log by something other than date.
    private func readingsSection(_ snapshot: HistorySnapshot) -> some View {
        Section {
            NavigationLink(value: HistoryRoute.focus) {
                LabeledContent("Priorities") {
                    let pinned = (focus.state.value ?? []).filter(\.isPinned).count
                    Text(pinned == 0 ? "None pinned" : "\(pinned) pinned").monospacedDigit()
                }
            }
            NavigationLink(value: HistoryRoute.movements) {
                LabeledContent("Movements") {
                    Text("\(snapshot.movements.entries.count)").monospacedDigit()
                }
            }
            NavigationLink(value: HistoryRoute.records) {
                LabeledContent("Personal records") {
                    let held = snapshot.records.reduce(0) { $0 + $1.records.count }
                    Text(held == 0 ? "None yet" : "\(held)").monospacedDigit()
                }
            }
            NavigationLink(value: HistoryRoute.trends) {
                Text("Load and top sets")
            }
        }
    }

    /// One week: its head opens the week, and each day in it opens the day.
    private func weekSection(_ week: SessionLog.Week) -> some View {
        Section {
            ForEach(week.days) { day in
                NavigationLink(value: HistoryRoute.day(day.day)) {
                    DayLogRow(day: day, today: today)
                }
                .contextMenu { menu(for: day) }
                .swipeActions(edge: .trailing) { swipes(for: day) }
            }
        } header: {
            WeekHeader(week: week)
        }
    }

    /// A run of weeks with nothing logged, as one line between the weeks either side.
    private func quietSection(from first: CalendarDay, weeks count: Int) -> some View {
        Section {
            Text("\(Formatting.count(count, "week")) with nothing logged · \(Formatting.span(from: first, to: first.adding(days: 7 * count - 1)))")
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    /// A day's long press: each session's movements, actions and copy, a section per session when
    /// the day held more than one.
    @ViewBuilder
    private func menu(for day: SessionLog.Day) -> some View {
        ForEach(day.sessions) { session in
            Section(day.sessions.count > 1 ? session.headline : "") {
                menu(for: session)
            }
        }
    }

    /// The delete swipe, offered only on a day holding one session, where it cannot mean the
    /// wrong one.
    @ViewBuilder
    private func swipes(for day: SessionLog.Day) -> some View {
        if day.sessions.count == 1, let session = day.sessions.first {
            swipes(for: session)
        }
    }

    /// The long press: open any of the session's movements, act on the session, or copy its line —
    /// which is what gets it into a message or a question in Chat.
    @ViewBuilder
    private func menu(for session: LoggedSession) -> some View {
        let movements = session.lines.compactMap { line in
            line.movementDataId.map { (id: $0, name: line.movementName) }
        }
        if !movements.isEmpty {
            Section {
                ForEach(movements, id: \.id) { movement in
                    // A button that pushes, since a link inside a context menu does not navigate.
                    Button(movement.name) { path.append(HistoryRoute.movement(movement.id)) }
                }
            }
        }
        ForEach(transitions(on: session.kind)) { transition in
            Button(role: transition.isDestructive ? .destructive : nil) {
                ask(transition, about: session)
            } label: {
                Label(transition.label, systemImage: transition.systemImage)
            }
        }
        Button {
            Clipboard.copy(summary(of: session))
        } label: {
            Label("Copy", systemImage: "doc.on.doc")
        }
    }

    /// The quick one. Only the delete swipes: a trailing swipe is iOS's delete gesture, and
    /// offering two destructive swipes on one row turns an easy gesture into a choice.
    @ViewBuilder
    private func swipes(for session: LoggedSession) -> some View {
        if transitions(on: session.kind).contains(.delete) {
            Button(role: .destructive) {
                ask(.delete, about: session)
            } label: {
                Label("Delete", systemImage: "trash")
            }
        }
    }

    /// What History itself can carry out. Starting is dropped: it hands the device a workout to log
    /// into, and the logging surface is reached from Train.
    private func transitions(on kind: LoggedSessionKind) -> [SessionTransition] {
        SessionTransition.offered(on: kind).filter { $0 != .start }
    }

    /// One session as text, for the pasteboard: the day and what it came to, then each movement's
    /// line, then the notes.
    private func summary(of session: LoggedSession) -> String {
        let day = Formatting.day(RelativeDay.of(session.day, today: today))
        let head = [day, Formatting.sessionFacts(session.facts)].compactMap { $0 }.joined(separator: " · ")
        let lines = session.lines.map { "\($0.movementName) \(MovementLines.line($0.line))" }
        return ([head] + lines + (session.hasNotes ? [session.notes] : [])).joined(separator: "\n")
    }

    // MARK: - Empty and not loaded

    private var nothingLoggedYet: some View {
        Section {
            ContentUnavailableView {
                Text("No sessions logged")
            } description: {
                Text("Completed sessions are listed here by month, newest first.")
            }
            .listRowBackground(Color.clear)
        }
    }

    private var searchFoundNothing: some View {
        Section {
            ContentUnavailableView.search(text: query.nonEmpty ?? tokens.map(\.name).joined(separator: ", "))
                .listRowBackground(Color.clear)
        }
    }

    @ViewBuilder
    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("History is not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await refresh() } }
            }
        }
        .listRowBackground(Color.clear)
    }

    /// Today from the device's calendar, read once per body evaluation: "is this yesterday" is a
    /// question about the athlete's day, not about a session.
    private var today: CalendarDay { CalendarDay(Date()) }
}

/// A movement chosen to filter the log by, as a search token.
struct MovementToken: Identifiable, Hashable {
    let id: UUID
    let name: String
}

/// A week's head: its dates, which open the week, and how many sessions it held — the figure the
/// athlete's first goal is measured in.
private struct WeekHeader: View {
    let week: SessionLog.Week

    var body: some View {
        HStack(alignment: .firstTextBaseline) {
            NavigationLink(value: HistoryRoute.week(week.start)) {
                HeaderLinkLabel(Text(Formatting.week(startingOn: week.start)))
            }
            .accessibilityHint("Opens the week")
            Spacer()
            Text(Formatting.count(week.performedCount, "session"))
                .monospacedDigit()
        }
    }
}

/// One day in the log: its date, then each session as a log-book entry.
private struct DayLogRow: View {
    let day: SessionLog.Day
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            ForEach(Array(day.sessions.enumerated()), id: \.element.id) { index, session in
                LogRow(session: session, today: today, showsDay: index == 0)
            }
        }
    }
}

/// One session in the log.
///
/// A session that happened reads as a log-book entry: the day and how long, each movement's line,
/// and what it came to. Planned, missed and skipped sessions keep the shorter row with a state mark,
/// since they have no work to list and their state is the news.
private struct LogRow: View {
    let session: LoggedSession
    let today: CalendarDay
    /// Whether the row leads with the day — the first session of a day does, a second does not.
    var showsDay = true

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        switch session.kind {
        case .completed:
            entry
        case .inProgress, .skipped, .missed, .planned, .unrecognised:
            StateRow(session: session, today: today)
        }
    }

    private var entry: some View {
        let heading = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline))

        return VStack(alignment: .leading, spacing: 5) {
            heading {
                Text(showsDay ? Formatting.date(session.day) : (session.startedAt.map { "Then, at \($0.formatted(date: .omitted, time: .shortened))" } ?? "Then"))
                    .font(.subheadline.weight(.semibold))
                if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
                if case .performed(_, let duration?, _) = session.facts {
                    Text(Formatting.duration(seconds: duration))
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            if !session.lines.isEmpty {
                MovementLines(digests: session.lines)
            }
            Text(Formatting.totals(session.totals))
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
            if session.hasNotes {
                Text(session.notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(2)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }
}

/// A session that is not a finished one: its state mark, what it was, and the day.
private struct StateRow: View {
    let session: LoggedSession
    let today: CalendarDay

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: session.kind.systemImage)
                .foregroundStyle(tint)
                .font(.title3)
                .accessibilityHidden(true)

            VStack(alignment: .leading, spacing: 3) {
                Text(session.headline)
                    .font(.headline)
                    .lineLimit(2)
                Text(subtitle)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                if session.hasNotes {
                    Text(session.notes)
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                        .lineLimit(2)
                }
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(session.kind.label). \(session.headline). \(subtitle)")
    }

    private var subtitle: String {
        let day = Formatting.day(RelativeDay.of(session.day, today: today))
        guard let facts = Formatting.sessionFacts(session.facts) else { return day }
        return "\(day) · \(facts)"
    }

    /// The semantic system colours, so a state reads the way it does elsewhere in iOS.
    private var tint: Color {
        switch session.kind {
        case .completed: .green
        case .inProgress: .orange
        case .skipped: Color(.tertiaryLabel)
        case .missed: .red
        case .planned: .accentColor
        case .unrecognised: Color(.tertiaryLabel)
        }
    }
}
