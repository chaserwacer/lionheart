import SwiftUI

/// Body's sections for what Apple's devices measured: the phone's gait, a row into the watch's
/// history, and the sync that brings them in.
///
/// Gait leads because it is current — the phone measures every walk — and it is the figure that
/// moves when a leg is hurt.
struct HealthMeasuresSections: View {
    @Environment(HealthMeasuresStore.self) private var store
    @Environment(HealthMeasureSyncStore.self) private var sync

    @State private var isExplained = false

    var body: some View {
        let history = store.history
        let today = CalendarDay(Date())
        let gait = HealthMetric.gaitFigures.compactMap { metric in
            history.principal(metric).map { (metric: metric, days: $0.days) }
        }

        Section {
            if case .loading = store.state {
                SkeletonRows(count: 2)
            }
            ForEach(gait, id: \.metric) { figure in
                NavigationLink {
                    MeasureHistoryView(metric: figure.metric)
                } label: {
                    MeasureRow(metric: figure.metric, days: figure.days, today: today)
                }
            }
            if !HealthMetric.watchFigures.allSatisfy({ history.days($0, by: .appleWatch).isEmpty }) {
                NavigationLink {
                    AppleWatchHistoryView()
                } label: {
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Apple Watch history")
                        Text("VO₂max, resting heart rate, HRV (SDNN), breathing")
                            .font(.subheadline)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
            HealthMeasureSyncRow(store: sync)
        } header: {
            HStack {
                Text("Gait")
                Spacer()
                // The sheet hangs on the button: a Section's modifiers reach every row, not one host.
                Button("What these are", systemImage: "info.circle") { isExplained = true }
                    .labelStyle(.iconOnly)
                    .buttonStyle(.borderless)
                    .sheet(isPresented: $isExplained) {
                        MeasureInfoSheet(metrics: HealthMetric.gaitFigures, title: "Gait")
                    }
            }
        } footer: {
            if !gait.isEmpty {
                Text("Measured by your iPhone on every walk. Four-week mean, and its change against the twelve weeks before.")
            }
        }
        .task { await store.refreshIfNeeded() }
    }
}
