import Charts
import SwiftUI

/// A weekly count as quiet bars — sets for load, sessions for consistency — with its four-week
/// average drawn through them in ink.
///
/// The weeks gone by are Iron because a single week is noise next to the trend; the newest week,
/// the one still being trained, is Brick, the one thing on the chart to act on. Weeks with no
/// training stay on the axis as empty slots, so a fortnight off reads as one.
///
/// With its axes it is a chart to read week by week: pressing and dragging scrubs across the weeks,
/// marking the one under the finger with its count and the average there, a tick at each new week.
/// Without them it is a strip in a row that opens somewhere, and takes no touches of its own.
/// VoiceOver reads it as an audio graph of the weeks (`accessibilityChartDescriptor`).
struct WeeklyLoadChart: View {
    /// Oldest first, empty weeks included.
    let weeks: [TrainingWeek]
    /// The trailing average for the same weeks; it may start later than they do.
    let average: [TrainingTrends.WeeklyLoad]
    var counting: WeeklyCount = .sets
    var showsAxes = true

    @State private var pressed: Date?

    /// Which count the bars are.
    enum WeeklyCount {
        case sets
        case sessions

        var label: String {
            switch self {
            case .sets: "Sets"
            case .sessions: "Sessions"
            }
        }

        func of(_ week: TrainingWeek) -> Int {
            switch self {
            case .sets: week.setCount
            case .sessions: week.sessionCount
            }
        }
    }

    var body: some View {
        let selected = showsAxes ? week(at: pressed) : nil

        Chart {
            ForEach(weeks) { week in
                // A week-wide bar on a week-stepped axis. A bare date would plot each week as a
                // hairline at its first day under day labels, which reads as daily data.
                BarMark(
                    x: .value("Week", week.start.middayDate(), unit: .weekOfYear),
                    y: .value(counting.label, counting.of(week)))
                .foregroundStyle(fill(of: week, selected: selected))
            }
            ForEach(average) { load in
                LineMark(
                    x: .value("Week", load.start.middayDate(), unit: .weekOfYear),
                    y: .value("Four-week average", load.value))
                .interpolationMethod(.monotone)
                .lineStyle(StrokeStyle(lineWidth: 2))
                .foregroundStyle(Tokens.ink)
            }
            if let selected {
                RuleMark(x: .value("Week", selected.start.middayDate(), unit: .weekOfYear))
                    .foregroundStyle(.clear)
                    .annotation(
                        position: .top, spacing: 2,
                        overflowResolution: .init(x: .fit(to: .chart), y: .disabled)
                    ) {
                        readout(selected)
                    }
            }
        }
        .chartXSelection(value: showsAxes ? $pressed : .constant(nil))
        .chartXAxis {
            if showsAxes {
                AxisMarks(values: .automatic(desiredCount: 4)) { value in
                    AxisGridLine()
                    AxisValueLabel {
                        if let date = value.as(Date.self) {
                            Text(date, format: .dateTime.month(.abbreviated).day())
                        }
                    }
                }
            }
        }
        .chartYAxis(showsAxes ? .automatic : .hidden)
        .sensoryFeedback(.selection, trigger: selected?.id)
        .accessibilityLabel("\(counting.label) per week")
        .accessibilityValue(spoken)
        .accessibilityChartDescriptor(WeeklyLoadDescriptor(weeks: weeks, counting: counting))
    }

    /// The newest week in Brick, the rest in Iron, the pressed one at full strength.
    private func fill(of week: TrainingWeek, selected: TrainingWeek?) -> Color {
        if week.id == weeks.last?.id { return Tokens.accent }
        return Tokens.iron.opacity(week.id == selected?.id ? 1 : 0.38)
    }

    /// The week a pressed date falls in.
    private func week(at date: Date?) -> TrainingWeek? {
        guard let date else { return nil }
        return weeks.last { $0.start.startDate() <= date }
    }

    private func readout(_ week: TrainingWeek) -> some View {
        let mean = average.first { $0.start == week.start }
        return VStack(alignment: .leading, spacing: 0) {
            Text(week.start.middayDate(), format: .dateTime.month(.abbreviated).day())
                .foregroundStyle(Color(.secondaryLabel))
            Text("\(counting.of(week)) \(counting.label.lowercased())")
                .fontWeight(.semibold)
            if let mean {
                Text("avg \(Formatting.measure(mean.value))")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .font(.caption.monospacedDigit())
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(Color(.secondarySystemGroupedBackground), in: .rect(cornerRadius: 8))
    }

    private var spoken: String {
        guard let latest = average.last else { return "" }
        let trained = weeks.filter { $0.sessionCount > 0 }.count
        return "\(Formatting.count(trained, "week")) trained of \(weeks.count). Four-week average \(Formatting.measure(latest.value)) \(counting.label.lowercased())."
    }
}

/// The weeks as VoiceOver's audio graph and data table: one value per week, by its Monday.
private struct WeeklyLoadDescriptor: AXChartDescriptorRepresentable {
    let weeks: [TrainingWeek]
    let counting: WeeklyLoadChart.WeeklyCount

    func makeChartDescriptor() -> AXChartDescriptor {
        let counts = weeks.map { Double(counting.of($0)) }
        let names = weeks.map { $0.start.middayDate().formatted(.dateTime.month(.abbreviated).day()) }
        let x = AXCategoricalDataAxisDescriptor(title: "Week of", categoryOrder: names)
        let y = AXNumericDataAxisDescriptor(
            title: counting.label,
            range: 0...max(1, counts.max() ?? 1),
            gridlinePositions: []
        ) { "\(Int($0)) \(counting.label.lowercased())" }
        let series = AXDataSeriesDescriptor(
            name: "\(counting.label) per week",
            isContinuous: false,
            dataPoints: zip(names, counts).map { AXDataPoint(x: $0, y: $1) })
        return AXChartDescriptor(
            title: "\(counting.label) per week", summary: nil, xAxis: x, yAxis: y, additionalAxes: [],
            series: [series])
    }
}
