import SwiftUI

/// What have I done?
///
/// The session log is the screen, because that is the question: an inset-grouped `List` of sessions
/// newest first, grouped by month, searchable. Records and trends are the two readings *of* that
/// log, so they are destinations above it rather than a segmented control fighting the search
/// field for the top of the screen.
struct HistoryView: View {
    let store: HistoryStore

    @State private var query = ""

    /// Where History can navigate. One `navigationDestination` per value type, registered once —
    /// register two for the same type and the later one silently wins.
    private enum Route: Hashable {
        case session(UUID)
        case records
        case trends
    }

    var body: some View {
        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    LoadingRow()
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let snapshot):
                    content(snapshot, staleBecause: nil)
                case .stale(let snapshot, let failure):
                    content(snapshot, staleBecause: failure)
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("History")
            .searchable(text: $query, prompt: "Movement or note")
            .minimizingSearchField()
            .refreshable { await store.refresh() }
            .navigationDestination(for: Route.self) { destination(for: $0) }
        }
        .task { await store.refresh() }
    }

    @ViewBuilder
    private func destination(for route: Route) -> some View {
        switch route {
        case .session(let id):
            if let snapshot = store.state.value, let session = snapshot.session(id) {
                SessionDetailView(session: session, catalogue: snapshot.catalogue)
            } else {
                // Reachable: a refresh can land while a pushed row's session is no longer in the
                // window. Saying so beats an empty screen with a title.
                ContentUnavailableView(
                    "Session not available",
                    systemImage: "questionmark.folder",
                    description: Text("It is no longer in the window History has loaded."))
            }
        case .records:
            PersonalRecordsView(records: store.state.value?.records ?? [])
        case .trends:
            TrendsView(trends: store.state.value?.trends ?? TrainingTrends(weeks: [], strength: []))
        }
    }

    // MARK: - Loaded

    @ViewBuilder
    private func content(_ snapshot: HistorySnapshot, staleBecause failure: LoadFailure?) -> some View {
        if let failure {
            StaleBanner(failure: failure)
        }

        let log = snapshot.log.filtered(by: query)

        if isSearching {
            if log.isEmpty {
                searchFoundNothing
            } else {
                months(of: log)
            }
        } else {
            readings(snapshot)
            if snapshot.log.isEmpty {
                nothingLoggedYet
            } else {
                months(of: log)
            }
        }
    }

    /// Records and trends. Hidden while searching, because a search is about the log and two
    /// navigation rows above the results would be noise in the way.
    @ViewBuilder
    private func readings(_ snapshot: HistorySnapshot) -> some View {
        Section {
            NavigationLink(value: Route.records) {
                LabeledContent {
                    Text(snapshot.records.isEmpty ? "None yet" : "\(snapshot.records.count)")
                        .monospacedDigit()
                } label: {
                    Label("Personal records", systemImage: "trophy")
                }
            }
            NavigationLink(value: Route.trends) {
                Label("Trends", systemImage: "chart.xyaxis.line")
            }
        }
    }

    @ViewBuilder
    private func months(of log: SessionLog) -> some View {
        ForEach(log.months) { month in
            Section(Formatting.month(month.start)) {
                ForEach(month.sessions) { session in
                    NavigationLink(value: Route.session(session.sessionId)) {
                        SessionRow(session: session, today: today)
                    }
                }
            }
        }
    }

    // MARK: - Empty and not loaded

    private var nothingLoggedYet: some View {
        Section {
            ContentUnavailableView {
                Label("Nothing logged yet", systemImage: "list.bullet.rectangle")
            } description: {
                Text("Sessions you complete show up here, newest first, with their sets.")
            }
            .listRowBackground(Color.clear)
        }
    }

    private var searchFoundNothing: some View {
        Section {
            ContentUnavailableView.search(text: query)
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
                Button("Try again") { Task { await store.refresh() } }
            }
        }
        .listRowBackground(Color.clear)
    }

    private var isSearching: Bool {
        !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    /// Today from the device's calendar, read once per body evaluation: "is this yesterday" is a
    /// question about the athlete's day, not about a session.
    private var today: CalendarDay { CalendarDay(Date()) }
}

/// One session in the log.
private struct SessionRow: View {
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
