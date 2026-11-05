import Charts
import SwiftUI

/// The score as a partition: the whole on top, its four parts beneath, and every measurement under
/// its part, each cell as wide as the points it could earn and filled with the points it did.
///
/// One axis for all three rows, so a measurement sits exactly under the part it belongs to and the
/// part under the share of the whole it is: a gap in the bottom row reads straight up to the part
/// and the number it cost. The cells lean at the mark's angle, so the card reads as Tempo's own
/// figure, and only the score itself is Brick — this week, the figure to move — with the parts in
/// ink and the measurements in Iron beneath it. The fills rise from nothing when the chart appears
/// and spring to a new figure when the score changes, the opening's motion. Tapping a part opens
/// it. Hidden from VoiceOver in favour of the part rows beside it, which state the same figures in
/// words.
struct ScoreBreakdownChart: View {
    let score: TrainingScore
    let parts: [ScorePartBreakdown]
    var onSelect: (ScorePart) -> Void = { _ in }

    @ScaledMetric(relativeTo: .caption) private var rowHeight: CGFloat = 26
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    /// Whether the fills have risen to their figures, which they do once, on first appearing.
    @State private var hasRisen = false

    /// The gap between neighbouring cells, in score points.
    private static let gap = 0.35

    private enum Tier: String, Plottable, CaseIterable {
        case whole = "Score", part = "Parts", measure = "Measures"

        var fill: Color {
            switch self {
            case .whole: Tokens.accent
            case .part: Tokens.ink
            case .measure: Tokens.iron
            }
        }
    }

    var body: some View {
        Chart {
            RuleMark(x: .value("Excellent", WeeklyScoreChart.excellent))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [2, 2]))
                .foregroundStyle(Color(.tertiaryLabel))
            cell(.whole, start: 0, outOf: Double(score.outOf), share: Double(score.value) / Double(max(1, score.outOf)), isKnown: true)
            ForEach(parts) { part in
                cell(.part, start: part.start, outOf: part.outOf, share: part.share, isKnown: part.points != nil)
                    .annotation(position: .top, alignment: .leading, spacing: 2) {
                        if !dynamicTypeSize.isAccessibilitySize {
                            Text(part.part.name)
                                .font(.caption2.monospacedDigit())
                                .foregroundStyle(Color(.secondaryLabel))
                                .lineLimit(1)
                                .minimumScaleFactor(0.7)
                        }
                    }
                ForEach(part.cells) { cell in
                    self.cell(.measure, start: cell.start, outOf: cell.outOf, share: cell.share, isKnown: cell.points != nil)
                }
            }
        }
        .chartXScale(domain: 0...100)
        .chartYScale(domain: Tier.allCases.map(\.rawValue))
        .chartXAxis(.hidden)
        .chartYAxis(.hidden)
        .chartLegend(.hidden)
        .frame(height: rowHeight * 3 + 14)
        .chartOverlay { proxy in
            GeometryReader { geometry in
                Rectangle()
                    .fill(.clear)
                    .contentShape(Rectangle())
                    .onTapGesture { location in
                        guard let frame = proxy.plotFrame,
                              let x: Double = proxy.value(atX: location.x - geometry[frame].origin.x),
                              let part = parts.last(where: { $0.start <= x })
                        else { return }
                        onSelect(part.part)
                    }
            }
        }
        .animation(reduceMotion ? nil : .brandRise(), value: score)
        .onAppear {
            guard !hasRisen else { return }
            if reduceMotion {
                hasRisen = true
            } else {
                withAnimation(.brandRise()) { hasRisen = true }
            }
        }
        .accessibilityHidden(true)
    }

    /// A cell's track across the points it could earn, and its fill across the points it did. An
    /// unknown cell is a track alone, so it reads as out of reach rather than as nothing earned.
    @ChartContentBuilder
    private func cell(_ tier: Tier, start: Double, outOf: Double, share: Double, isKnown: Bool) -> some ChartContent {
        let from = start + Self.gap
        let width = max(0, outOf - 2 * Self.gap)
        BarMark(
            xStart: .value("Start", from),
            xEnd: .value("End", from + width),
            y: .value("Row", tier.rawValue),
            height: .ratio(tier == .whole ? 0.62 : 0.5))
            .foregroundStyle(isKnown ? Color(.tertiarySystemFill) : Color(.quaternarySystemFill))
            .clipShape(LeanedBar())
        // Drawn even at nothing, so the rise has a mark to grow from.
        if isKnown {
            BarMark(
                xStart: .value("Start", from),
                xEnd: .value("End", from + width * share * (hasRisen ? 1 : 0)),
                y: .value("Row", tier.rawValue),
                height: .ratio(tier == .whole ? 0.62 : 0.5))
                .foregroundStyle(tier.fill)
                .clipShape(LeanedBar())
        }
    }
}
