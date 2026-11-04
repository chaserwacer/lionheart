import Charts
import SwiftUI

/// One metric over the workout's clock time — power, cadence, speed, a run's form — as a line with
/// its mean ruled across it.
struct MetricTraceChart: View {
    let readout: MetricReadout
    let unit: RunUnit
    let start: Date
    let end: Date

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 140

    var body: some View {
        let points = MetricTrace.drawn(readout.samples)
        let average = WorkoutMetricText.plotted(readout.metric, readout.average, in: unit)

        Chart {
            ForEach(Array(points.enumerated()), id: \.offset) { _, point in
                LineMark(
                    x: .value("Time", point.at),
                    y: .value(WorkoutMetricText.unitLabel(readout.metric, in: unit),
                              WorkoutMetricText.plotted(readout.metric, point.value, in: unit)))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 1.2))
                    .foregroundStyle(Color(.label))
            }
            RuleMark(y: .value("Mean", average))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(Color.accentColor)
        }
        .chartXScale(domain: start...max(end, start.addingTimeInterval(1)))
        // The spread is what reads — a cadence of 80 to 92 drawn from nought is a flat line.
        .chartYScale(domain: .automatic(includesZero: false))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                AxisGridLine()
                AxisValueLabel(format: .dateTime.hour().minute())
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 4)
    }
}
