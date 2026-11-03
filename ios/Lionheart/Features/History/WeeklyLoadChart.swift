import Charts
import SwiftUI

/// A weekly count as quiet bars — sets for load, sessions for consistency — with its four-week
/// average drawn through them in the accent.
///
/// The bars are grey because a single week is noise next to the trend, and the line is the one
/// thing on the chart in colour. Weeks with no training stay on the axis as empty slots, so a
/// fortnight off reads as one.
struct WeeklyLoadChart: View {
    /// Oldest first, empty weeks included.
    let weeks: [TrainingWeek]
    /// The trailing average for the same weeks; it may start later than they do.
    let average: [TrainingTrends.WeeklyLoad]
    var counting: WeeklyCount = .sets
    var showsAxes = true

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
        Chart {
            ForEach(weeks) { week in
                // A week-wide bar on a week-stepped axis. A bare date would plot each week as a
                // hairline at its first day under day labels, which reads as daily data.
                BarMark(
                    x: .value("Week", week.start.middayDate(), unit: .weekOfYear),
                    y: .value(counting.label, counting.of(week)))
                .foregroundStyle(Color(.systemFill))
            }
            ForEach(average) { load in
                LineMark(
                    x: .value("Week", load.start.middayDate(), unit: .weekOfYear),
                    y: .value("Four-week average", load.value))
                .interpolationMethod(.monotone)
                .lineStyle(StrokeStyle(lineWidth: 2))
                .foregroundStyle(Tokens.accent)
            }
        }
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
        .accessibilityLabel("\(counting.label) per week")
        .accessibilityValue(spoken)
    }

    private var spoken: String {
        guard let latest = average.last else { return "" }
        let trained = weeks.filter { $0.sessionCount > 0 }.count
        return "\(Formatting.count(trained, "week")) trained of \(weeks.count). Four-week average \(Formatting.measure(latest.value)) \(counting.label.lowercased())."
    }
}
