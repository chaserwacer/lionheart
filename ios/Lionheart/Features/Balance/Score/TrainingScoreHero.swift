import SwiftUI

/// Train's hero: where the week stands and what to do about it — the score against the weeks before
/// it, the partition of what went into it, and what is up next — on Graphite.
///
/// The parts in words, each opening its screen, follow in a section of their own
/// (`TrainingScorePartsSection`), so the hero stays one glance tall. The partition's bars rise to
/// their figures when the score changes, and hitting a target this week is felt as well as seen.
struct TrainingScoreHero: View {
    let score: TrainingScore
    /// The weeks before, oldest first, for the context beside the number; empty before they load.
    let weeks: [WeeklyScore]
    /// What is next, when it has been read.
    let next: NextSessions?
    /// Opens a part from a tap on the partition.
    let onSelect: (ScorePart) -> Void

    @State private var isInfoShown = false
    /// Counts the reads that hit a target, for the haptic.
    @State private var targetsHit = 0
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    /// The page's appearance, read outside the hero's forced dark one.
    @Environment(\.colorScheme) private var colorScheme

    var body: some View {
        HeroSection {
            VStack(alignment: .leading, spacing: 14) {
                headline
                ScoreBreakdownChart(score: score, parts: ScoreBreakdown.of(score), onSelect: onSelect)
                if score.isPartial {
                    Text("\(unknownNames) unknown this week, so the score is out of \(score.outOf).")
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            .padding(.vertical, 6)
            // On the row: a sheet hung on the section never presents. It takes the page's
            // appearance back from the hero's dark one.
            .sheet(isPresented: $isInfoShown) {
                ScoreInfoSheet(part: nil, score: score)
                    .environment(\.colorScheme, colorScheme)
            }
            .onChange(of: score) { previous, current in
                if !ScoreTargets.newlyMet(from: previous, to: current).isEmpty { targetsHit += 1 }
            }
            .sensoryFeedback(.success, trigger: targetsHit)
            if let next {
                NextUpLine(next: next)
            }
        }
    }

    /// What the figures cover and the method; the score; its context — beside the figures, or under
    /// them at the accessibility sizes, where side by side the context would wrap a word to a line.
    private var headline: some View {
        let stacked = dynamicTypeSize.isAccessibilitySize
        return VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text("The week · 7 days to \(Formatting.dayInline(score.window.to))")
                    .font(.meta)
                    .foregroundStyle(Color(.secondaryLabel))
                Spacer(minLength: 8)
                Button("How the score is worked out", systemImage: "info.circle") { isInfoShown = true }
                    .labelStyle(.iconOnly)
                    .buttonStyle(.borderless)
            }
            let layout = stacked
                ? AnyLayout(VStackLayout(alignment: .leading, spacing: 6))
                : AnyLayout(HStackLayout(alignment: .lastTextBaseline, spacing: 12))
            layout {
                // A second figure for the same week sits beside this one, in this row.
                HStack(alignment: .lastTextBaseline, spacing: 20) {
                    HeroFigure(
                        label: "Score", figure: "\(score.value)", unit: "of \(score.outOf)",
                        value: Double(score.value))
                }
                if !stacked { Spacer(minLength: 0) }
                if let trend = ScoreTrend.of(weeks) {
                    VStack(alignment: stacked ? .leading : .trailing, spacing: 1) {
                        Text("\(trend.weeks)-week median \(trend.median)")
                        Text("best \(trend.best)")
                    }
                    .font(.meta)
                    .foregroundStyle(Color(.secondaryLabel))
                    .accessibilityElement(children: .ignore)
                    .accessibilityLabel("Median of \(trend.weeks) weeks \(trend.median), best \(trend.best)")
                }
            }
        }
    }

    private var unknownNames: String {
        score.unknown.map(\.name).joined(separator: " and ").capitalisingFirst
    }
}
