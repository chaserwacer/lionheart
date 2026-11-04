import SwiftUI

/// The Apple Watch's fitness and recovery figures over the years it was worn: VO₂max, resting heart
/// rate, HRV as SDNN, breathing in sleep and walking heart rate.
///
/// Their own screen rather than lines on Oura's charts: the watch's history predates the ring's, and
/// its HRV is a different statistic from Oura's, so a shared axis would draw a change of device as
/// a change in the athlete.
struct AppleWatchHistoryView: View {
    @Environment(HealthMeasuresStore.self) private var store

    @State private var isExplained = false

    var body: some View {
        List {
            switch store.state {
            case .loading:
                Section { SkeletonRows(count: 5) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let history):
                content(history)
            case .stale(let history, let staleness):
                StaleBanner(staleness)
                content(history)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Apple Watch")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("What these are", systemImage: "info.circle") { isExplained = true }
            }
        }
        .sheet(isPresented: $isExplained) {
            MeasureInfoSheet(metrics: HealthMetric.watchFigures, title: "Apple Watch")
        }
        .refreshable { await store.refresh() }
        .task { await store.refreshIfNeeded() }
    }

    @ViewBuilder
    private func content(_ history: HealthMeasureHistory) -> some View {
        let today = CalendarDay(Date())
        let measured = HealthMetric.watchFigures.filter { !history.days($0, by: .appleWatch).isEmpty }

        if measured.isEmpty {
            ContentUnavailableView {
                Text("No Apple Watch readings")
            } description: {
                Text("Health holds no VO₂max, resting heart rate or HRV measured by an Apple Watch.")
            }
            .listRowBackground(Color.clear)
        } else {
            Section {
                ForEach(measured, id: \.self) { metric in
                    NavigationLink {
                        MeasureHistoryView(metric: metric)
                    } label: {
                        MeasureRow(metric: metric, days: history.days(metric, by: .appleWatch), today: today)
                    }
                }
            } header: {
                Text(Self.span(of: measured.flatMap { history.days($0, by: .appleWatch) }))
            } footer: {
                Text("HRV here is SDNN; Oura's is RMSSD. Different statistics, never compared.")
            }
        }
    }

    /// "2019 to 2024": the years the watch measured anything.
    private static func span(of days: [MeasuredDay]) -> String {
        guard let first = days.map(\.day).min(), let last = days.map(\.day).max() else { return "" }
        return first.year == last.year ? "\(first.year)" : "\(first.year) to \(last.year)"
    }
}
