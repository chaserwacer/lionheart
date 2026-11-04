import SwiftUI

/// What is my body doing?
///
/// An inset-grouped `List` that leads with the latest readiness, then the last night and the latest
/// day's scores, then the questions with screens of their own — cardio, daily movement and body
/// composition — and finally the records behind it all: the wearable's days, the readings the
/// athlete entered, and the injuries being tracked.
struct BodyView: View {
    let store: BodyStore
    let ouraSync: OuraSyncStore
    let cardio: CardioStore
    let habits: HabitsStore
    let wellness: WellnessStore
    let composition: BodyCompositionStore
    let bodyweight: BodyweightStore

    /// The runs imported from Apple Health, which the cardio screen opens onto.
    @Environment(RunStore.self) private var runs

    @State private var isNewInjuryPresented = false
    @State private var isWellnessPresented = false
    @State private var isAddingScan = false

    var body: some View {
        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    loadingSections
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
            .navigationTitle("Body")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    // What is logged by hand here: how the day feels, an injury, and a scan's sheet.
                    Menu {
                        Button("Log wellness") { isWellnessPresented = true }
                        Button("New injury") { isNewInjuryPresented = true }
                        Button("Add scan") { isAddingScan = true }
                    } label: {
                        Label("Add", systemImage: "plus")
                    }
                }
            }
            .sheet(isPresented: $isNewInjuryPresented) {
                NewInjurySheet(store: store)
            }
            .sheet(isPresented: $isAddingScan) {
                ScanEntrySheet(store: composition, bodyweight: bodyweight)
            }
            .sheet(isPresented: $isWellnessPresented) {
                WellnessEntrySheet(store: wellness, existing: store.state.value?.wellness ?? [], since: store.firstDay)
            }
            .refreshable { await store.refresh() }
            .navigationDestination(for: BodyRoute.self) { destination(for: $0) }
            // An injury action is the only thing on this tab that writes, and a 404 — the injury
            // was deleted on the web app — looks exactly like a row that did nothing.
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
        }
        .task { await store.refreshIfNeeded() }
        // The two rows that summarise a screen of their own read that screen's default span, once.
        .task {
            if cardio.reports[.quarter] == nil { await cardio.load(.quarter) }
        }
        .task {
            if habits.reports[.month] == nil { await habits.load(.month) }
        }
        .task { await composition.refreshIfNeeded() }
    }

    @ViewBuilder
    private func destination(for route: BodyRoute) -> some View {
        let snapshot = store.state.value
        switch route {
        case .ouraDays:
            OuraDaysView(days: snapshot?.ouraDays ?? [], refresh: { await store.refresh() })
        case .ouraDay(let day):
            if let snapshot, let ouraDay = snapshot.ouraDays.first(where: { $0.date == day }) {
                OuraDayView(
                    day: ouraDay,
                    night: snapshot.mainSleepEvent(on: day),
                    days: snapshot.ouraDays,
                    workouts: snapshot.workouts(on: day),
                    tags: snapshot.tags(on: day))
            } else {
                notLoaded("That day is no longer loaded")
            }
        case .night(let day):
            if let night = snapshot?.mainSleepEvent(on: day) {
                NightView(night: night)
            } else {
                notLoaded("That night is no longer loaded")
            }
        case .cardio:
            CardioView(store: cardio)
        case .cardioSession(let id, let span):
            if let report = cardio.reports[span]?.value, let session = report.sessions.first(where: { $0.id == id }) {
                CardioSessionView(session: session, maxHeartRate: report.maxHeartRate, store: cardio)
            } else {
                notLoaded("That session is no longer loaded")
            }
        case .run(let id):
            RunView(trainingSessionId: id, store: runs)
        case .habits:
            HabitsView(store: habits)
        case .dayActivity(let day):
            DayActivityView(day: day, store: habits)
        case .wellness:
            WellnessHistoryView(store: wellness)
        case .injuries:
            InjuriesView(injuries: snapshot?.injuries ?? [], store: store)
        case .composition:
            BodyCompositionView(store: composition, bodyweight: bodyweight)
        case .scan(let id):
            if let scan = composition.history.scans.first(where: { $0.id == id }) {
                ScanDetailView(scan: scan, store: composition, bodyweight: bodyweight)
            } else {
                notLoaded("That scan is no longer loaded")
            }
        case .bodyweight:
            BodyweightView(store: bodyweight)
        case .injury(let id):
            if let injury = snapshot?.injuries.first(where: { $0.injuryId == id }) {
                InjuryDetailView(history: InjuryHistory.of(injury), store: store)
            } else {
                notLoaded("That injury is no longer loaded")
            }
        }
    }

    private func notLoaded(_ title: String) -> some View {
        ContentUnavailableView {
            Text(title)
        } description: {
            Text("Pull to refresh on the Body screen.")
        }
    }

    // MARK: - Loaded

    @ViewBuilder
    private func content(_ snapshot: BodySnapshot, staleBecause staleness: Staleness?) -> some View {
        if let staleness {
            StaleBanner(staleness)
        }

        recoverySection(snapshot)
        latestSection(snapshot)
        questionsSection
        activeInjuriesSection(snapshot)
        HealthMeasuresSections()
        recordsSection(snapshot)

        if snapshot.isEmpty {
            nothingRecorded
        }
    }

    /// The latest verdict and the sync control. The figures behind the verdict are Today's; here it
    /// heads the records it is drawn from.
    @ViewBuilder
    private func recoverySection(_ snapshot: BodySnapshot) -> some View {
        Section("Recovery") {
            if let recovery = snapshot.latestRecovery(today: today), let readiness = recovery.readiness {
                RecoveryVerdictView(
                    readiness: readiness, figure: recovery.readinessFigure, shownDay: today, today: today)
            } else {
                Text("No Oura readiness in the last eight weeks.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
            OuraSyncRow(store: ouraSync, today: today)
        }
    }

    /// The last night and the newest scored day, each opening onto its own screen.
    @ViewBuilder
    private func latestSection(_ snapshot: BodySnapshot) -> some View {
        let night = snapshot.latestNight(onOrBefore: today)
        let day = snapshot.ouraDays.first { $0.readiness?.score != nil || $0.sleep?.score != nil }
        if night != nil || day != nil {
            Section {
                if let night {
                    NavigationLink(value: BodyRoute.night(night.day)) {
                        NightSummaryRow(night: night)
                    }
                }
                if let day {
                    NavigationLink(value: BodyRoute.ouraDay(day.date)) {
                        QuestionRow(title: "Oura's scores and contributors", detail: Self.scores(of: day).nonEmpty)
                    }
                }
            } header: {
                Text(night.map { "Night of \(Formatting.date($0.day.adding(days: -1)))" } ?? "Latest")
            }
        }
    }

    private static func scores(of day: OuraDay) -> String {
        [
            day.readiness?.score.map { "Readiness \($0)" },
            day.sleep?.score.map { "Sleep \($0)" },
            day.activity?.score.map { "Activity \($0)" },
        ].compactMap(\.self).joined(separator: " · ")
    }

    /// Cardio and daily movement: questions with screens of their own, each summarised in a line.
    private var questionsSection: some View {
        Section {
            NavigationLink(value: BodyRoute.cardio) {
                QuestionRow(title: "Cardio", detail: cardioLine)
            }
            NavigationLink(value: BodyRoute.habits) {
                QuestionRow(title: "Daily movement", detail: movementLine)
            }
            NavigationLink(value: BodyRoute.composition) {
                QuestionRow(title: "Body composition", detail: compositionLine)
            }
        }
    }

    private var compositionLine: String? {
        guard let scan = composition.history.latest else {
            return composition.state.value == nil ? nil : "No scan recorded"
        }
        let sheet = scan.sheet
        return "\(Formatting.date(scan.day)) · \(SheetFigure.mass(sheet.weight, sheet.unit)) · "
            + "muscle \(SheetFigure.number(sheet.skeletalMuscleMass)) · fat \(SheetFigure.percent(sheet.percentBodyFat))"
    }

    /// The aerobic base's two minute figures, in words: a week's means over the last four weeks.
    private var cardioLine: String? {
        guard let base = cardio.reports[.quarter]?.value?.aerobicBase else { return nil }
        let figures = [
            base.cardioMinutesPerWeek.recent.map { "\(Int($0.rounded())) min of cardio" },
            base.zone2MinutesPerWeek.recent.map { "\(Int($0.rounded())) in zone 2" },
        ].compactMap(\.self)
        return figures.isEmpty ? nil : "A week, last 4 weeks: " + figures.joined(separator: ", ")
    }

    private var movementLine: String? {
        guard let weekdays = habits.reports[.month]?.value?.current.weekdays else { return nil }
        let figures = [
            weekdays.steps.map { "\(Int($0).formatted()) steps" },
            weekdays.longestSittingMinutes.map { "longest sitting \(Formatting.minutes($0))" },
        ].compactMap(\.self)
        return figures.isEmpty ? nil : "Mean weekday, last 30 days: " + figures.joined(separator: ", ")
    }

    @ViewBuilder
    private func activeInjuriesSection(_ snapshot: BodySnapshot) -> some View {
        let active = snapshot.activeInjuries
        if !active.isEmpty {
            Section("Being tracked") {
                ForEach(active) { injury in
                    let history = InjuryHistory.of(injury)
                    NavigationLink(value: BodyRoute.injury(injury.injuryId)) {
                        InjuryRow(history: history, today: today)
                    }
                    .injuryActions(on: history, store: store)
                }
            }
        }
    }

    @ViewBuilder
    private func recordsSection(_ snapshot: BodySnapshot) -> some View {
        Section("Records") {
            NavigationLink(value: BodyRoute.ouraDays) {
                LabeledContent {
                    Text(count(snapshot.ouraDays.count, "day")).monospacedDigit()
                } label: {
                    Text("Oura days")
                }
            }
            NavigationLink(value: BodyRoute.wellness) {
                LabeledContent {
                    Text(snapshot.wellness.first.map { "Last rated \(Formatting.date($0.date))" }
                         ?? "None in 8 weeks")
                        .monospacedDigit()
                } label: {
                    Text("Wellness")
                }
            }
            NavigationLink(value: BodyRoute.injuries) {
                LabeledContent {
                    Text(count(snapshot.injuries.count, "injury", plural: "injuries")).monospacedDigit()
                } label: {
                    Text("Injuries")
                }
            }
        }
    }

    private func count(_ value: Int, _ singular: String, plural: String? = nil) -> String {
        value == 0 ? "None" : Formatting.count(value, singular, plural: plural)
    }

    // MARK: - Empty and not loaded

    /// The tab's own shape while the first read is out: a recovery card and the three records
    /// behind it. Six requests in parallel, and this is what stands in for all of them.
    @ViewBuilder
    private var loadingSections: some View {
        Section("Recovery") {
            SkeletonRows(count: 1)
            OuraSyncRow(store: ouraSync, today: today)
        }
        questionsSection
        Section("Records") { SkeletonRows(count: 3) }
    }

    private var nothingRecorded: some View {
        Section {
            ContentUnavailableView {
                Text("No body data")
            } description: {
                Text("Oura days arrive when the ring syncs. Wellness entries and injuries are entered by hand.")
            }
            .listRowBackground(Color.clear)
        }
    }

    @ViewBuilder
    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Body is not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await store.refresh() } }
            }
        }
        .listRowBackground(Color.clear)
    }

    private var today: CalendarDay { CalendarDay(Date()) }
}

/// A row that opens a screen of its own: its name, and a line of the figures it leads with once
/// they have been read.
private struct QuestionRow: View {
    let title: String
    let detail: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
            if let detail {
                Text(detail)
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 1)
    }
}
