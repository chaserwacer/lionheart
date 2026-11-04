import Charts
import SwiftUI

/// Five segments against the analyser's reference: a bar per segment to its percentage, a rule at
/// 100, and the mass printed at each bar's end.
///
/// Drawn as a chart rather than the sheet's five bar rows because the reading is the pattern —
/// which segments sit above the reference and by how much, and whether left matches right — and a
/// shared axis is what makes that comparison at a glance.
struct SegmentChart: View {
    let segments: Segments<SegmentReading>
    let unit: WeightUnit

    @ScaledMetric(relativeTo: .body) private var rowHeight: CGFloat = 30
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        // At the accessibility sizes the axis labels and the bar-end figures cannot share a row,
        // so the same readings stand as labelled rows instead.
        if typeSize.isAccessibilitySize {
            VStack(alignment: .leading, spacing: 8) {
                ForEach(BodySegment.allCases, id: \.self) { segment in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(segment.title)
                        Text("\(SheetFigure.mass(segments[segment].mass, unit)) · \(SheetFigure.percent(segments[segment].percentOfReference))")
                            .monospacedDigit()
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                    .accessibilityElement(children: .combine)
                }
            }
        } else {
            chart
        }
    }

    private var chart: some View {
        Chart {
            RuleMark(x: .value("Reference", 100))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(Color(.secondaryLabel))
            ForEach(BodySegment.allCases, id: \.self) { segment in
                let reading = segments[segment]
                BarMark(
                    xStart: .value("Reference", 100),
                    xEnd: .value("Percent", reading.percentOfReference),
                    y: .value("Segment", segment.title))
                    .foregroundStyle(Tokens.accent.opacity(segment == .trunk ? 0.55 : 0.85))
                    .annotation(position: reading.percentOfReference >= 100 ? .trailing : .leading, spacing: 4) {
                        Text("\(SheetFigure.number(Imperial.pounds(reading.mass, from: unit))) · \(SheetFigure.percent(reading.percentOfReference))")
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
            }
        }
        .chartXScale(domain: domain)
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { value in
                AxisGridLine()
                AxisValueLabel { if let percent = value.as(Double.self) { Text("\(Int(percent))%") } }
            }
        }
        .chartYAxis {
            AxisMarks { _ in AxisValueLabel(centered: true) }
        }
        .frame(height: rowHeight * 5 + 24)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(BodySegment.allCases.map { segment in
            "\(segment.title) \(SheetFigure.mass(segments[segment].mass, unit)), \(SheetFigure.number(segments[segment].percentOfReference)) percent of reference"
        }.joined(separator: "; "))
    }

    /// Wide enough to hold every bar and its label, and always the reference.
    private var domain: ClosedRange<Double> {
        let percents = BodySegment.allCases.map { segments[$0].percentOfReference }
        let low = min(percents.min() ?? 100, 100), high = max(percents.max() ?? 100, 100)
        let room = max(high - low, 20) * 0.45
        return (low - (low < 100 ? room : 5))...(high + room)
    }
}
