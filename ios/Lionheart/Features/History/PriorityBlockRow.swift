import SwiftUI

/// A pinned lift as History's hero carries it: where the horizon stands, the best estimated single
/// since the pin, and the block itself — each week's best as a leaning bar, the weeks gone in Iron,
/// this week in Brick, the weeks to come as outlines — so three weeks building and one back read
/// as the mark drawn from his own training.
struct PriorityBlockRow: View {
    let reading: FocusReading
    let block: BlockWeeks?
    let name: String

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .largeTitle) private var barsHeight: CGFloat = 46

    var body: some View {
        let stacked = dynamicTypeSize.isAccessibilitySize
        VStack(alignment: .leading, spacing: 8) {
            let heading = stacked
                ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
                : AnyLayout(HStackLayout(alignment: .firstTextBaseline))
            heading {
                Text(name)
                    .font(.headline)
                if !stacked { Spacer(minLength: 8) }
                Text(FocusWords.phase(reading.phase))
                    .font(.meta)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            let layout = stacked
                ? AnyLayout(VStackLayout(alignment: .leading, spacing: 8))
                : AnyLayout(HStackLayout(alignment: .bottom, spacing: 16))
            layout {
                if let progress = reading.progress, let best = progress.best {
                    HeroFigure(
                        label: "Best e1RM",
                        figure: Formatting.measure(Imperial.pounds(best.value, from: progress.unit).rounded()),
                        unit: Imperial.weightLabel(progress.unit),
                        value: best.value,
                        style: .title)
                        .fixedSize()
                }
                if let block, !block.weeks.isEmpty {
                    LeaningBars(bars: bars(of: block))
                        .frame(height: barsHeight)
                        .frame(maxWidth: .infinity)
                }
            }
            Text(([reading.progress.flatMap(change)] + [FocusWords.summary(reading)]).compactMap(\.self).joined(separator: " · "))
                .font(.meta)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
    }

    /// "+12 lb since the pin", in the reading's unit read as pounds.
    private func change(_ progress: FocusReading.Progress) -> String? {
        progress.change.map { change in
            let pounds = Formatting.measure(Imperial.pounds(abs(change), from: progress.unit).rounded())
            return "\(change < 0 ? "−" : "+")\(pounds) \(Imperial.weightLabel(progress.unit)) since the pin"
        }
    }

    private func bars(of block: BlockWeeks) -> [LeaningBars.Bar] {
        zip(block.weeks, block.heights).enumerated().map { index, pair in
            let (week, height) = pair
            let role: LeaningBars.Role = switch week {
            case .done(.some): .before
            case .done(.none): .empty
            // This week stays Brick even before its first session, as a stub to fill.
            case .current: .now
            case .ahead: .ahead
            }
            return LeaningBars.Bar(id: "\(index)", height: height ?? 0, role: role)
        }
    }
}
