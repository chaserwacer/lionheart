import SwiftUI

/// The training score in full: the card Train leads with, then half a year of weeks against 90.
///
/// Lionheart's score, not Oura's: it is earned by doing the training rather than by moving, so a
/// decent week reads in the 60s and 70s and 90 takes every part at once. Each part opens onto its
/// measurements, and a part with no data says it is unknown rather than showing zero.
struct TrainingScoreView: View {
    @Environment(TrainingBalanceStore.self) private var balance
    /// Opens a part from a tap on the partition; the rows open their part themselves.
    let open: (ScorePart) -> Void

    @ScaledMetric(relativeTo: .body) private var trendHeight: CGFloat = 140

    var body: some View {
        List {
            switch balance.score {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Score not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await balance.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let score):
                content(score)
            case .stale(let score, let staleness):
                StaleBanner(staleness)
                content(score)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Training score")
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await balance.refresh() }
        .task { await balance.refreshIfNeeded() }
    }

    @ViewBuilder
    private func content(_ score: TrainingScore) -> some View {
        let weeks = balance.weeks.value ?? []
        TrainingScoreCard(score: score, weeks: weeks, onSelect: open)
        if !weeks.isEmpty {
            Section {
                WeeklyScoreChart(weeks: weeks, highlighted: weeks.last?.window.to)
                    .frame(height: trendHeight)
                    .padding(.vertical, 6)
            } header: {
                Text("Weeks")
            } footer: {
                Text(trendNote(weeks))
            }
        }
    }

    private func trendNote(_ weeks: [WeeklyScore]) -> String {
        let excellent = weeks.filter { Double($0.value) >= WeeklyScoreChart.excellent }.count
        let partial = weeks.contains(where: \.isPartial) ? " Pale bars had a part unknown." : ""
        let reached = excellent == 0 ? "none has reached it" : "\(Formatting.count(excellent, "week")) reached it"
        return "Each week to its Sunday, the last in colour. The dashed line is 90; \(reached).\(partial)"
    }
}
