import SwiftUI

/// The training score as Train leads with it: the number against the weeks before it, the
/// partition of everything that went into it, and one row per part opening that part.
///
/// The partition carries the shape — which part, which measurement, cost the points — and the rows
/// carry the figures in words, so the section reads at a glance and to VoiceOver alike. Its own
/// section, so the score screen and Train draw the same card.
struct TrainingScoreCard: View {
    let score: TrainingScore
    /// The weeks before, oldest first, for the context beside the number; empty before they load.
    let weeks: [WeeklyScore]
    /// Opens a part from a tap on the partition. The rows open their part themselves.
    var onSelect: (ScorePart) -> Void = { _ in }

    @State private var isInfoShown = false
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let parts = ScoreBreakdown.of(score)
        Section {
            VStack(alignment: .leading, spacing: 12) {
                headline
                ScoreBreakdownChart(score: score, parts: parts, onSelect: onSelect)
            }
            .padding(.vertical, 4)
            ForEach(parts) { part in
                NavigationLink {
                    ScorePartView(part: part.part)
                } label: {
                    PartRow(part: part)
                }
            }
        } header: {
            HStack {
                Text("Training score")
                Spacer()
                Button("How the score is worked out", systemImage: "info.circle") { isInfoShown = true }
                    .labelStyle(.iconOnly)
            }
        } footer: {
            if score.isPartial {
                Text("\(unknownNames) unknown this week: its points are out of reach, not zero, so the score reads out of \(score.outOf).")
            }
        }
        .sheet(isPresented: $isInfoShown) {
            ScoreInfoSheet(part: nil, score: score)
        }
    }

    /// The number beside its context, or above it at the accessibility sizes, where side by side
    /// the context would wrap a word to a line.
    private var headline: some View {
        let stacked = dynamicTypeSize.isAccessibilitySize
        let layout = stacked
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 4))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))
        return layout {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text("\(score.value)")
                    .font(.largeTitle.weight(.bold))
                    .monospacedDigit()
                Text("of \(score.outOf)")
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if !stacked { Spacer(minLength: 0) }
            VStack(alignment: stacked ? .leading : .trailing, spacing: 2) {
                Text("7 days to \(Formatting.dayInline(score.window.to))")
                if let trend = ScoreTrend.of(weeks) {
                    Text("\(trend.weeks)-week median \(trend.median) · best \(trend.best)")
                }
            }
            .font(.caption.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
            .multilineTextAlignment(stacked ? .leading : .trailing)
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(spoken)
    }

    private var spoken: String {
        var words = "Training score \(score.value) of \(score.outOf), seven days to \(Formatting.dayInline(score.window.to))."
        if let trend = ScoreTrend.of(weeks) {
            words += " Median of \(trend.weeks) weeks \(trend.median), best \(trend.best)."
        }
        return words
    }

    private var unknownNames: String {
        score.unknown.map(\.name).joined(separator: " and ")
    }
}

/// One part as the row that opens it: its name, its measurements in a line, and its points.
private struct PartRow: View {
    let part: ScorePartBreakdown

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))
        layout {
            VStack(alignment: .leading, spacing: 2) {
                Text(part.part.name)
                    .font(.subheadline.weight(.semibold))
                Text(part.summary)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
            Text(ScorePoints.earned(part.points, of: part.outOf))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(part.points == nil ? Color(.secondaryLabel) : Color(.label))
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(part.part.name)
        .accessibilityValue("\(ScorePoints.spoken(part.points, of: part.outOf)). \(part.summary)")
    }
}
