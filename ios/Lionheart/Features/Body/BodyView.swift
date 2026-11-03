import SwiftUI

/// What is my body doing?
///
/// An inset-grouped `List` that leads with the latest readiness and then opens into the three
/// records behind it: the wearable's days, the readings the athlete entered, and the injuries being
/// tracked.
///
/// The Oura surface is being widened in parallel, so this screen draws a day from the **panels it
/// carries** rather than from a fixed set of rows: a new panel on the wire becomes a new case of
/// `OuraPanel` and a new section here, not a redesign.
struct BodyView: View {
    let store: BodyStore
    let ouraSync: OuraSyncStore

    @State private var isNewInjuryPresented = false

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
                case .stale(let snapshot, let failure):
                    content(snapshot, staleBecause: failure)
                }
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
            .navigationTitle("Body")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("New injury", systemImage: "plus") { isNewInjuryPresented = true }
                }
            }
            .sheet(isPresented: $isNewInjuryPresented) {
                NewInjurySheet(store: store)
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
                    night: snapshot.mainSleep(on: day),
                    workouts: snapshot.workouts(on: day),
                    tags: snapshot.tags(on: day))
            } else {
                ContentUnavailableView {
                    Text("That day is no longer loaded")
                } description: {
                    Text("Pull to refresh on the Body screen.")
                }
            }
        case .wellness:
            WellnessHistoryView(readings: snapshot?.wellness ?? [], refresh: { await store.refresh() })
        case .injuries:
            InjuriesView(injuries: snapshot?.injuries ?? [], store: store)
        case .injury(let id):
            if let injury = snapshot?.injuries.first(where: { $0.injuryId == id }) {
                InjuryDetailView(history: InjuryHistory.of(injury), store: store)
            } else {
                ContentUnavailableView {
                    Text("That injury is no longer loaded")
                } description: {
                    Text("Pull to refresh on the Body screen.")
                }
            }
        }
    }

    // MARK: - Loaded

    @ViewBuilder
    private func content(_ snapshot: BodySnapshot, staleBecause failure: LoadFailure?) -> some View {
        if let failure {
            StaleBanner(failure: failure)
        }

        recoverySection(snapshot)
        activeInjuriesSection(snapshot)
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
            OuraSyncRow(store: ouraSync, today: today)
            if let recovery = snapshot.latestRecovery(today: today), let readiness = recovery.readiness {
                RecoveryVerdictView(
                    readiness: readiness, figure: recovery.readinessFigure, shownDay: today, today: today)
            } else {
                Text("No Oura readiness in the last eight weeks.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
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
                    Text(count(snapshot.wellness.count, "entry", plural: "entries")).monospacedDigit()
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
            OuraSyncRow(store: ouraSync, today: today)
            SkeletonRows(count: 1)
        }
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
