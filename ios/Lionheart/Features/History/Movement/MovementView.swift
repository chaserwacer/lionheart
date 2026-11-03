import SwiftUI

/// One movement: every session it was done in, how its numbers have moved, what it holds, and how
/// often it is trained.
///
/// Strong's three pages — History, Charts, Records — behind a segmented control, under a summary
/// that is always there: how many sessions, how often a week, when last, and the best estimated
/// single. Frequency leads the summary rather than hiding in a tab, because for the athlete's goals
/// how often a lift is trained is the number that has most often been the problem.
///
/// Everything is read from the whole log History already holds, so the page costs no request of its
/// own except the records the movement established, which mark the sets in its history.
struct MovementView: View {
    let movementDataId: UUID
    let history: HistoryStore
    let focus: FocusStore
    let library: MovementLibraryStore

    @State private var page: Page = .history
    @State private var asked: PersonalRecord?
    @State private var opened: RecordKey?
    @State private var editing: FocusPinSheet.Editing?

    enum Page: CaseIterable, Identifiable {
        case history
        case charts
        case records

        var id: Self { self }

        var label: String {
            switch self {
            case .history: "History"
            case .charts: "Charts"
            case .records: "Records"
            }
        }
    }

    var body: some View {
        let today = CalendarDay(Date())
        let catalogue = history.state.value?.catalogue ?? library.catalogue
        let name = catalogue.name(for: movementDataId)

        List {
            switch history.state {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await history.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let snapshot):
                content(snapshot, name: name, today: today)
            case .stale(let snapshot, let staleness):
                StaleBanner(staleness)
                content(snapshot, name: name, today: today)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .environment(\.defaultMinListRowHeight, 34)
        .navigationTitle(name)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar { focusButton(name: name) }
        .refreshable {
            await history.refresh()
            await history.loadEstablished(on: movementDataId)
        }
        .task(id: movementDataId) {
            await history.refreshIfNeeded()
            await focus.refreshIfNeeded()
            await history.loadEstablished(on: movementDataId)
        }
        .retractionDialog(asking: $asked, name: name, store: history)
        .navigationDestination(item: $opened) { key in
            RecordHistoryView(key: key, name: name, store: history)
        }
        .sheet(item: $editing) { editing in
            FocusPinSheet(editing: editing, focus: focus, library: library)
        }
    }

    // MARK: - Content

    @ViewBuilder
    private func content(_ snapshot: HistorySnapshot, name: String, today: CalendarDay) -> some View {
        let movement = snapshot.history(of: movementDataId, today: today)
        let held = snapshot.records(on: .movement(movementDataId))

        if movement.isEmpty && held == nil {
            ContentUnavailableView {
                Text("Not performed yet")
            } description: {
                Text("\(name) has no completed sessions. Its history, charts and records start with the first.")
            }
            .listRowBackground(Color.clear)
        } else {
            summary(movement, today: today)
            focusSection(movement, today: today, catalogue: snapshot.catalogue)

            Section {
                Picker("Page", selection: $page) {
                    ForEach(Page.allCases) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            }

            switch page {
            case .history:
                MovementSessionsSections(
                    movement: movement,
                    marks: marks(for: movement),
                    today: today)
            case .charts:
                MovementChartSections(movement: movement, today: today)
            case .records:
                if let held {
                    SubjectRecordsSections(held: held, name: name, ask: { asked = $0 }, open: { opened = $0 })
                } else {
                    Section {
                        Text("No standing records.").foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
        }
    }

    private func marks(for movement: MovementHistory) -> RecordMarks {
        let records = history.established[.movement(movementDataId)]?.value ?? []
        return RecordMarks.of(records, in: movement.sessions.map {
            RecordMarks.Session(sessionId: $0.sessionId, startedAt: $0.startedAt, sets: $0.sets)
        })
    }

    /// How often, how recently, and how strong — the figures every visit to a movement starts from.
    @ViewBuilder
    private func summary(_ movement: MovementHistory, today: CalendarDay) -> some View {
        let frequency = MovementFrequency.of(movement, today: today)
        let unit = movement.units.first

        Section {
            SummaryFigures(figures: [
                ("Sessions", "\(frequency.sessionCount)"),
                ("Per week", Formatting.rate(frequency.perWeek)),
                ("Last \(MovementFrequency.recentWeeks) weeks", Formatting.rate(frequency.recentPerWeek)),
            ])
            .padding(.vertical, 2)
            if let last = movement.lastDay {
                LabeledContent("Last done") {
                    Text(Formatting.day(RelativeDay.of(last, today: today)))
                }
            }
            if let unit, let best = movement.series(.estimatedOneRepMax, in: unit).max(by: { $0.value < $1.value }) {
                LabeledContent {
                    Text(Formatting.estimate(best.value, unit)).monospacedDigit()
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("Best estimated 1RM")
                        Text("\(Formatting.weight(best.weight, unit)) × \(best.reps) · \(Formatting.date(best.day, today: today))")
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
        } footer: {
            if let since = frequency.since {
                Text("Per week since the first session, \(Formatting.date(since, today: today)). Estimated 1RM is Epley, from sets of 12 reps or fewer.")
            }
        }
    }

    /// The pins on this movement that are being worked on, each read against its own horizon.
    @ViewBuilder
    private func focusSection(_ movement: MovementHistory, today: CalendarDay, catalogue: MovementCatalogue) -> some View {
        let current = focus.pins(on: movementDataId)
            .map { FocusReading.of($0, history: movement, today: today) }
            .filter(\.phase.isCurrent)

        if !current.isEmpty {
            Section("Focus") {
                ForEach(current) { reading in
                    FocusReadingRows(reading: reading, showsLastDone: false)
                }
            }
        }
    }

    @ToolbarContentBuilder
    private func focusButton(name: String) -> some ToolbarContent {
        ToolbarItem(placement: .topBarTrailing) {
            let current = focus.pins(on: movementDataId).first(where: \.isPinned)
            Button(current == nil ? "Pin" : "Edit focus") {
                editing = current.map(FocusPinSheet.Editing.revising)
                    ?? .pinning(movementDataId: movementDataId, name: name)
            }
        }
    }
}

// MARK: - History

/// Every session of the movement, newest first: the day and the session's best single, then each
/// set, with the ones that set a record marked.
struct MovementSessionsSections: View {
    let movement: MovementHistory
    let marks: RecordMarks
    let today: CalendarDay

    var body: some View {
        ForEach(movement.sessions) { session in
            Section {
                ForEach(Array(session.sets.enumerated()), id: \.element.id) { position, set in
                    SetRow(
                        number: position + 1,
                        entry: set.entry,
                        record: marks.outcome(of: .init(sessionId: session.sessionId, order: set.order)))
                }
            } header: {
                NavigationLink(value: HistoryRoute.session(session.sessionId)) {
                    HeaderLinkLabel(Text(Formatting.date(session.day, today: today))) {
                        if let estimate = estimate(of: session) {
                            Text(estimate).monospacedDigit()
                        }
                    }
                }
                .accessibilityHint("Opens the session")
            } footer: {
                if marks.setSessionVolume(in: session.sessionId, on: .movement(movement.movementDataId)) {
                    Text("Best session volume.")
                }
            }
        }
    }

    /// The session's best estimated single, in the unit the movement is lifted in now.
    private func estimate(of session: MovementHistory.Session) -> String? {
        guard let unit = movement.units.first,
              let best = MovementHistory.best(.estimatedOneRepMax, of: session.lifts(in: unit))
        else { return nil }
        return "e1RM \(Formatting.estimateFigure(best.value))"
    }
}
