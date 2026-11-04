import Charts
import SwiftUI

/// The span's logged runs as a fitness trend: what each kilometre cost the heart, run by run, over
/// the pace and average heart rate behind it.
///
/// Drawn only from runs with distance and heart rate over most of their time; a run without either
/// is a session above but not a reading here, and the footer says how many there were.
struct CardioRunsSection: View {
    let report: CardioReport

    @AppStorage("lh.runUnit") private var unit: RunUnit = .kilometres
    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 140

    var body: some View {
        let readings = RunReading.of(report)
        let runs = report.sessions.filter { if case .loggedSession(_, isTrailRun: true, _) = $0.source { true } else { false } }
        if !runs.isEmpty {
            Section {
                if readings.count >= Self.fewestForTrend {
                    chart(readings)
                }
                ForEach(readings.reversed()) { reading in
                    row(reading)
                }
                if readings.isEmpty {
                    Text("No run in this span has heart rate over most of its time.")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } header: {
                Text("Runs: heartbeats per \(unit.abbreviation)")
            } footer: {
                Text(footer(readings: readings.count, runs: runs.count))
            }
        }
    }

    /// Fewer runs than this draw no line: two points make a slope out of one run's weather.
    private static let fewestForTrend = 3

    /// The scale spans at least 15 % either side of the median, so a few beats between runs read
    /// as the small change they are rather than filling the chart.
    private func domain(_ readings: [RunReading]) -> ClosedRange<Double> {
        let values = readings.map { $0.beats(per: unit) }.sorted()
        let median = values[values.count / 2]
        return min(values[0], median * 0.85)...max(values[values.count - 1], median * 1.15)
    }

    private func chart(_ readings: [RunReading]) -> some View {
        Chart {
            ForEach(readings) { reading in
                LineMark(x: .value("Day", reading.start), y: .value("Beats", reading.beats(per: unit)))
                    .interpolationMethod(.monotone)
                    .foregroundStyle(Tokens.accent)
                PointMark(x: .value("Day", reading.start), y: .value("Beats", reading.beats(per: unit)))
                    .symbolSize(24)
                    .foregroundStyle(Tokens.accent)
            }
        }
        .chartYScale(domain: domain(readings))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                AxisGridLine()
                AxisValueLabel(format: .dateTime.month(.abbreviated).year(.twoDigits))
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 6)
        .accessibilityLabel("Heartbeats per \(unit.abbreviation) by run")
        .accessibilityValue(spoken(readings))
    }

    private func row(_ reading: RunReading) -> some View {
        HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 2) {
                Text(reading.start, format: .dateTime.month(.abbreviated).day().year())
                    .font(.subheadline)
                Text("\(unit.distance(reading.kilometres * 1000)) · \(unit.pace(perKilometre: reading.secondsPerKilometre)) · avg \(reading.averageBpm) bpm")
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Spacer()
            Text(reading.beats(per: unit).formatted(.number.precision(.fractionLength(0))))
                .font(.subheadline.weight(.semibold).monospacedDigit())
        }
        .accessibilityElement(children: .combine)
    }

    private func footer(readings: Int, runs: Int) -> String {
        let left = runs - readings
        let without = left > 0 ? " \(Formatting.count(left, "run")) without distance or enough heart rate \(left == 1 ? "is" : "are") left out." : ""
        return "Average heart rate times minutes per \(unit.abbreviation): what a \(unit == .kilometres ? "kilometre" : "mile") cost the heart, at elapsed pace. Roughly level across easy and steady paces, so it falls as the aerobic base grows.\(without)"
    }

    private func spoken(_ readings: [RunReading]) -> String {
        guard let first = readings.first, let last = readings.last else { return "" }
        let digits = FloatingPointFormatStyle<Double>.number.precision(.fractionLength(0))
        return "\(Formatting.count(readings.count, "run")), from \(first.beats(per: unit).formatted(digits)) to \(last.beats(per: unit).formatted(digits)) beats."
    }
}
