import SwiftUI

/// A week's hard sets and training score on History's week page: push, pull and legs against their
/// targets, the compound patterns, and the score with its measurements, opening the full volume.
///
/// Read over the seven days ending on the week's Sunday — or today, for the week in progress — the
/// same window Train reads, so a week here and the week on Train agree when they are the same week.
struct WeekBalanceSections: View {
    let state: LoadState<TrainingBalanceStore.WeekBalance>

    var body: some View {
        switch state {
        case .loading:
            Section("Hard sets") { SkeletonRows(count: 3) }
        case .unavailable(let failure):
            Section("Hard sets") {
                Text(failure.message).foregroundStyle(Color(.secondaryLabel))
            }
        case .loaded(let week), .stale(let week, _):
            content(week)
        }
    }

    @ViewBuilder
    private func content(_ week: TrainingBalanceStore.WeekBalance) -> some View {
        Section {
            ForEach(week.volume.families) { family in
                VolumeRow(
                    label: family.family.name, sets: Double(family.hardSets), target: family.target,
                    lastTrained: family.lastTrained)
            }
            ForEach(week.volume.patterns.filter { $0.target != nil }) { pattern in
                LabeledContent(pattern.pattern.name) {
                    Text("\(pattern.hardSets) of \(pattern.target ?? 0)").monospacedDigit()
                }
            }
            NavigationLink {
                VolumeView(volume: week.volume)
            } label: {
                Text("Muscle groups")
            }
        } header: {
            Text("Hard sets")
        } footer: {
            Text("Working, drop and failure sets over the seven days to \(Formatting.date(week.volume.window.to)); warm-ups never count. Target 8 a family, 4 a compound pattern.")
        }
        Section {
            TrainingScoreSummary(score: week.score)
        } header: {
            Text("Training score")
        } footer: {
            Text(week.score.isPartial
                 ? "Read over \(week.score.outOf) points: \(week.score.unknown.map { $0.name.lowercased() }.joined(separator: ", ")) unknown."
                 : "Tempo's own score, out of 100. Train opens it in full.")
        }
    }
}
