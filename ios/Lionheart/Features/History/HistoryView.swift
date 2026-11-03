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
    @State private var asked: AskedTransition?

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
        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    Section("Sessions") { SkeletonRows(count: 6, systemImage: "checkmark.circle.fill") }
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
            .navigationDestination(for: HistoryRoute.self) { destination(for: $0) }
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
            "That did not happen",
            isPresented: Binding(
                get: { store.action.refusal != nil },
                set: { if !$0 { store.dismissRefusal() } })
        ) {
            Button("OK") { store.dismissRefusal() }
        } message: {
            if let refusal = store.action.refusal { Text(refusal.message) }
        }
        .task { await store.refresh() }
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

    @ViewBuilder
    private func destination(for route: HistoryRoute) -> some View {
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
            PersonalRecordsView(
                records: store.state.value?.records ?? [],
                catalogue: store.state.value?.catalogue ?? MovementCatalogue())
        case .recordSubject(let subject):
            SubjectRecordsView(
                subject: subject,
                held: store.state.value?.records(on: subject),
                catalogue: store.state.value?.catalogue ?? MovementCatalogue(),
                store: store)
        case .recordHistory(let key):
            RecordHistoryView(
                key: key,
                name: key.subject.name(in: store.state.value?.catalogue ?? MovementCatalogue()),
                store: store)
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
            NavigationLink(value: HistoryRoute.records) {
                LabeledContent {
                    let held = snapshot.records.reduce(0) { $0 + $1.records.count }
                    Text(held == 0 ? "None yet" : "\(held)")
                        .monospacedDigit()
                } label: {
                    Label("Personal records", systemImage: "trophy")
                }
            }
            NavigationLink(value: HistoryRoute.trends) {
                Label("Trends", systemImage: "chart.xyaxis.line")
            }
        }
    }

    @ViewBuilder
    private func months(of log: SessionLog) -> some View {
        ForEach(log.months) { month in
            Section(Formatting.month(month.start)) {
                ForEach(month.sessions) { session in
                    NavigationLink(value: HistoryRoute.session(session.sessionId)) {
                        SessionRow(session: session, today: today)
                    }
                    .contextMenu { menu(for: session) }
                    .swipeActions(edge: .trailing) { swipes(for: session) }
                }
            }
        }
    }

    /// The long press. Copying a session's own line is what gets it into a message or into a
    /// question in Chat, and is the one action every row can offer whatever state it is in.
    @ViewBuilder
    private func menu(for session: LoggedSession) -> some View {
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

    /// One session as a line of text, for the pasteboard: the day, what it was, and what it came to.
    private func summary(of session: LoggedSession) -> String {
        let day = Formatting.day(RelativeDay.of(session.day, today: today))
        let parts = [day, session.headline, Formatting.sessionFacts(session.facts)]
            .compactMap { $0 }
            .filter { !$0.isEmpty }
        guard session.hasNotes else { return parts.joined(separator: " · ") }
        return parts.joined(separator: " · ") + "\n" + session.notes
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
