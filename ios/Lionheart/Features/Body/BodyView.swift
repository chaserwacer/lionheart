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
            .navigationTitle("Body")
            .refreshable { await store.refresh() }
            .navigationDestination(for: BodyRoute.self) { destination(for: $0) }
        }
        .task { await store.refresh() }
    }

    @ViewBuilder
    private func destination(for route: BodyRoute) -> some View {
        let snapshot = store.state.value
        switch route {
        case .ouraDays:
            OuraDaysView(days: snapshot?.ouraDays ?? [])
        case .ouraDay(let day):
            if let snapshot, let ouraDay = snapshot.ouraDays.first(where: { $0.date == day }) {
                OuraDayView(
                    day: ouraDay,
                    night: snapshot.mainSleep(on: day),
                    workouts: snapshot.workouts(on: day),
                    tags: snapshot.tags(on: day))
            } else {
                ContentUnavailableView(
                    "That day is no longer loaded",
                    systemImage: "moon.zzz",
                    description: Text("Pull to refresh on the Body screen."))
            }
        case .wellness:
            WellnessHistoryView(readings: snapshot?.wellness ?? [])
        case .injuries:
            InjuriesView(injuries: snapshot?.injuries ?? [])
        case .injury(let id):
            if let injury = snapshot?.injuries.first(where: { $0.injuryId == id }) {
                InjuryDetailView(history: InjuryHistory.of(injury))
            } else {
                ContentUnavailableView(
                    "That injury is no longer loaded",
                    systemImage: "bandage",
                    description: Text("Pull to refresh on the Body screen."))
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

    @ViewBuilder
    private func recoverySection(_ snapshot: BodySnapshot) -> some View {
        Section("Recovery") {
            if let reading = snapshot.latestReadiness {
                HStack(alignment: .center, spacing: Tokens.cardPadding) {
                    ReadinessGauge(reading: reading)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(reading.band.label).font(.headline)
                        Text(Formatting.day(RelativeDay.of(reading.day, today: today)))
                            .font(.subheadline)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
                .padding(.vertical, 4)

                // Top-aligned so figures stay level when a label wraps at the larger Dynamic Type
                // sizes.
                // Contributor scores out of 100 rather than measurements — see `ReadinessReading`.
                HStack(alignment: .top, spacing: Tokens.cardPadding) {
                    if let restingHeartRate = reading.restingHeartRateBalance {
                        StatTile(value: "\(restingHeartRate)", label: "Resting HR balance")
                    }
                    if let hrvBalance = reading.hrvBalance {
                        StatTile(value: "\(hrvBalance)", label: "HRV balance")
                    }
                    if let sleep = reading.sleepScore {
                        StatTile(value: "\(sleep)", label: "Sleep")
                    }
                }
            } else {
                Label("The ring has recorded nothing in the last eight weeks", systemImage: "moon.zzz")
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
                    NavigationLink(value: BodyRoute.injury(injury.injuryId)) {
                        InjuryRow(history: InjuryHistory.of(injury), today: today)
                    }
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
                    Label("Oura days", systemImage: "bed.double")
                }
            }
            NavigationLink(value: BodyRoute.wellness) {
                LabeledContent {
                    Text(count(snapshot.wellness.count, "entry", plural: "entries")).monospacedDigit()
                } label: {
                    Label("Wellness", systemImage: "face.smiling")
                }
            }
            NavigationLink(value: BodyRoute.injuries) {
                LabeledContent {
                    Text(count(snapshot.injuries.count, "injury", plural: "injuries")).monospacedDigit()
                } label: {
                    Label("Injuries", systemImage: "bandage")
                }
            }
        }
    }

    private func count(_ value: Int, _ singular: String, plural: String? = nil) -> String {
        value == 0 ? "None" : Formatting.count(value, singular, plural: plural)
    }

    // MARK: - Empty and not loaded

    private var nothingRecorded: some View {
        Section {
            ContentUnavailableView {
                Label("Nothing about your body yet", systemImage: "heart.text.square")
            } description: {
                Text("Oura days arrive once the ring has synced. Wellness entries and injuries are yours to add.")
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

/// One injury in a list: what it is, how it is going, and since when.
struct InjuryRow: View {
    let history: InjuryHistory
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(history.injury.name.nonEmpty ?? "Unnamed injury")
                .font(.headline)
            Text(subtitle)
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "\(history.injury.name.nonEmpty ?? "Unnamed injury"). \(subtitle)")
    }

    private var subtitle: String {
        var parts = ["Since \(Formatting.date(history.injury.injuryDate))"]
        // Absent is not zero: nothing logged against it yet is a different thing from no pain.
        if let pain = history.latestPain { parts.append("last pain \(pain)/10") }
        if !history.injury.isActive { parts.append("resolved") }
        return parts.joined(separator: " · ")
    }
}
