import SwiftUI

/// What should I do, and how am I?
///
/// An inset-grouped `List`, which is what Fitness and Settings are made of: it gets separators,
/// Dynamic Type, swipe actions and both appearances right, and it is the fastest route to a screen
/// that looks like it belongs. Every decision on it — which readiness to show, which session counts
/// as the last one — is a pure function in `Core/Derived/`; this file chooses words and containers.
struct TodayView: View {
    let store: TodayStore
    /// Passed through to Settings, which states it. Carried rather than re-read from the bundle
    /// so there is one place configuration is resolved.
    let apiBaseURL: URL

    @State private var isQuickLogPresented = false
    @State private var isSettingsPresented = false

    var body: some View {
        NavigationStack {
            List {
                switch store.state {
                case .loading:
                    loadingRows
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let snapshot):
                    content(snapshot, staleBecause: nil)
                case .stale(let snapshot, let failure):
                    content(snapshot, staleBecause: failure)
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Today")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Settings", systemImage: "gearshape") { isSettingsPresented = true }
                }
            }
            .refreshable { await store.refresh() }
            .sheet(isPresented: $isQuickLogPresented) {
                QuickLogSheet(store: store)
            }
            .sheet(isPresented: $isSettingsPresented) {
                SettingsSheet(apiBaseURL: apiBaseURL)
            }
        }
        .task { await store.refresh() }
    }

    // MARK: - Loaded

    @ViewBuilder
    private func content(_ snapshot: TodaySnapshot, staleBecause failure: LoadFailure?) -> some View {
        if let failure {
            // The value stays; the banner says it could not be updated. Blanking a loaded screen
            // because a refresh failed is the thing LoadState exists to prevent.
            Section {
                Label(failure.message, systemImage: "arrow.triangle.2.circlepath.exclamationmark")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }

        bodySection(snapshot.readiness)
        quickLogSection
        if let active = snapshot.inProgress { inProgressSection(active) }
        lastSessionSection(snapshot.lastCompleted)
        nextSessionSection(snapshot.nextPlanned)
    }

    @ViewBuilder
    private func bodySection(_ reading: ReadinessReading?) -> some View {
        Section("How the body is") {
            if let reading {
                HStack(alignment: .center, spacing: Tokens.cardPadding) {
                    ReadinessGauge(reading: reading)
                    VStack(alignment: .leading, spacing: 4) {
                        Text(reading.band.label)
                            .font(.headline)
                        Text(Formatting.day(RelativeDay.of(reading.day, today: today)))
                            .font(.subheadline)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
                .padding(.vertical, 4)

                // Top-aligned so the figures line up when a label wraps to two lines at the larger
                // Dynamic Type sizes; centred, a one-line label drops its number below its
                // neighbours'.
                HStack(alignment: .top, spacing: Tokens.cardPadding) {
                    StatTile(value: "\(reading.restingHeartRate)", label: "Resting HR")
                    StatTile(value: "\(reading.hrvBalance)", label: "HRV balance")
                    if let sleep = reading.sleepScore {
                        StatTile(value: "\(sleep)", label: "Sleep")
                    }
                }
            } else {
                Label("No Oura data in the last two weeks", systemImage: "moon.zzz")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    private var quickLogSection: some View {
        Section {
            Button {
                isQuickLogPresented = true
            } label: {
                Label("Log what you did", systemImage: "text.bubble")
            }
        } footer: {
            Text("Type it in plain words and Lionheart works out the rest.")
        }
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

    @ViewBuilder
    private func lastSessionSection(_ session: TrainingSession.Completed?) -> some View {
        Section("Last session") {
            if let session {
                let summary = SessionSummary.of(session)
                VStack(alignment: .leading, spacing: 6) {
                    Text(Formatting.day(RelativeDay.of(summary.day, today: today)))
                        .font(.headline)
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
            } else {
                Text("Nothing logged yet.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    @ViewBuilder
    private func nextSessionSection(_ session: TrainingSession.Planned?) -> some View {
        Section("Next session") {
            if let session {
                let summary = PlanSummary.of(session)
                VStack(alignment: .leading, spacing: 6) {
                    Text(Formatting.day(RelativeDay.of(summary.plannedFor, today: today)))
                        .font(.headline)
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
            } else {
                Text("Nothing planned.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    private func detail(of summary: SessionSummary) -> String {
        var parts = [Formatting.count(summary.setCount, "set")]
        if summary.movementCount > 0 {
            parts.append(Formatting.count(summary.movementCount, "movement"))
        }
        if let duration = summary.duration {
            parts.append(Formatting.duration(seconds: duration))
        }
        if summary.hasEffort { parts.append("effort rated") }
        return parts.joined(separator: " · ")
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

    private var loadingRows: some View {
        Section {
            HStack(spacing: 10) {
                ProgressView()
                Text("Loading").foregroundStyle(Color(.secondaryLabel))
            }
        }
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

    /// Today, read once per body evaluation. It comes from the device's calendar rather than from a
    /// session, because the question "is this yesterday" is asked about the athlete's day.
    private var today: CalendarDay { CalendarDay(Date()) }
}
