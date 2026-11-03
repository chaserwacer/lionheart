import SwiftUI

/// One of Oura's scores and the components behind it, each drawn against the athlete's own range.
///
/// A component is Oura's 0–100 comparison of the day with his baseline, so the useful reading is
/// where today sits against where he usually sits — which a bare number cannot show and a gauge
/// per row can. The rows share fixed, Dynamic-Type-scaled columns, so the gauges line up down the
/// section and the eye can run down them the way it reads a table.
struct ContributorSection: View {
    let group: ContributorGroup
    let day: OuraDay
    /// The days around it, for each component's baseline. Only the four weeks before it are read.
    let days: [OuraDay]
    /// Figures that are measurements rather than components — temperature, steps — shown under them.
    var measurements: [OuraPanel.Figure] = []
    /// A word Oura gives instead of a score, such as resilience's level.
    var verdict: String?
    /// Whether this section's footer says how to read the gauges — once a screen, not under each.
    var explainsGauges = false

    var body: some View {
        let readings = ContributorReading.readings(of: group, on: day, among: days)
        if !readings.isEmpty || !measurements.isEmpty {
            Section {
                ForEach(readings) { reading in
                    ContributorRow(reading: reading)
                }
                if !measurements.isEmpty {
                    FigureGrid(figures: measurements)
                }
            } header: {
                header
            } footer: {
                if explainsGauges, readings.contains(where: { $0.baseline != nil }) {
                    Text("Oura's scores out of 100. Band: the 28 days before, mean ± one spread; tick: the mean.")
                }
            }
        }
    }

    private var header: some View {
        HStack(alignment: .firstTextBaseline) {
            Text(group.title)
            Spacer()
            if let score = group.score(on: day) {
                Text("\(score)").font(.headline.monospacedDigit()).foregroundStyle(Color(.label))
            } else if let verdict {
                Text(verdict).font(.subheadline).foregroundStyle(Color(.label))
            }
        }
    }
}

/// One component: its name, where it sits against his usual range, and the score.
private struct ContributorRow: View {
    let reading: ContributorReading

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .subheadline) private var nameWidth: CGFloat = 132
    @ScaledMetric(relativeTo: .subheadline) private var valueWidth: CGFloat = 30

    var body: some View {
        Group {
            if dynamicTypeSize.isAccessibilitySize {
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        name
                        Spacer()
                        value
                    }
                    gauge.frame(height: 14)
                }
            } else {
                HStack(spacing: 10) {
                    name.frame(width: nameWidth, alignment: .leading)
                    gauge.frame(height: 14)
                    value.frame(width: valueWidth, alignment: .trailing)
                }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(reading.contributor.name)
        .accessibilityValue(spoken)
    }

    private var name: some View {
        Text(reading.contributor.name)
            .font(.subheadline)
            .lineLimit(1)
            .minimumScaleFactor(0.85)
    }

    private var gauge: some View {
        RangeGauge(
            value: reading.value,
            usual: reading.usualRange,
            mean: reading.baseline?.mean,
            mark: standingColour)
    }

    private var value: some View {
        Text(reading.value.formatted(.number.precision(.fractionLength(0))))
            .font(.subheadline.weight(.semibold).monospacedDigit())
            .foregroundStyle(standingColour)
    }

    /// Orange only for a component outside his usual range on the worse side — the one colour on
    /// the screen that carries a verdict.
    private var standingColour: Color {
        switch reading.standing {
        case .unfavourable: .orange
        case .typical, .favourable, .noBaseline: Color(.label)
        }
    }

    private var spoken: String {
        let value = reading.value.formatted(.number.precision(.fractionLength(0)))
        guard let baseline = reading.baseline else { return value }
        let mean = baseline.mean.formatted(.number.precision(.fractionLength(0)))
        let standing = switch reading.standing {
        case .unfavourable: ", below your usual range"
        case .favourable: ", above your usual range"
        case .typical, .noBaseline: ""
        }
        return "\(value), usually \(mean)\(standing)"
    }
}
