import Foundation

/// How a recorded workout's metrics read on its page: their names, their figures, and which of
/// them the page leads with for the activity.
enum WorkoutMetricText {
    static func name(_ metric: WorkoutMetric, for activity: RecordedActivity) -> String {
        switch metric {
        case .power: activity.isRunning ? "Running power" : "Power"
        case .cadence: "Cadence"
        case .speed: activity.isRunning ? "Running speed" : "Speed"
        case .groundContactTime: "Ground contact"
        case .verticalOscillation: "Vertical oscillation"
        case .strideLength: "Stride length"
        case .heartRateRecovery: "HR recovery"
        case .unrecognised(let wire): wire
        }
    }

    /// A value of the metric in the unit it reads in. A run's speed reads as a pace per mile, a
    /// ride's in mph; the bounce in inches and the stride in feet, as a watch set to imperial units
    /// states them.
    static func figure(_ metric: WorkoutMetric, _ value: Double, for activity: RecordedActivity) -> String {
        switch metric {
        case .power: "\(Int(value.rounded())) W"
        case .cadence: "\(Int(value.rounded())) rpm"
        case .speed:
            activity.isRunning && value > 0
                ? Imperial.pace(perKilometre: 1000 / value)
                : Imperial.speed(metersPerSecond: value)
        case .groundContactTime: "\(Int(value.rounded())) ms"
        case .verticalOscillation: String(format: "%.1f in", Imperial.inches(meters: value / 100))
        case .strideLength: String(format: "%.1f ft", Imperial.feet(meters: value))
        case .heartRateRecovery: "\(Int(value.rounded())) bpm"
        case .unrecognised: String(format: "%.1f", value)
        }
    }

    /// The axis label for a trace of the metric: its unit alone.
    static func unitLabel(_ metric: WorkoutMetric) -> String {
        switch metric {
        case .power: "W"
        case .cadence: "rpm"
        case .speed: "mph"
        case .groundContactTime: "ms"
        case .verticalOscillation: "in"
        case .strideLength: "ft"
        case .heartRateRecovery: "bpm"
        case .unrecognised: ""
        }
    }

    /// The value a trace plots, in the unit its axis is labelled in.
    static func plotted(_ metric: WorkoutMetric, _ value: Double) -> Double {
        switch metric {
        case .speed: Imperial.milesPerHour(metersPerSecond: value)
        case .verticalOscillation: Imperial.inches(meters: value / 100)
        case .strideLength: Imperial.feet(meters: value)
        case .power, .cadence, .groundContactTime, .heartRateRecovery, .unrecognised:
            value
        }
    }

    /// The series worth a trace of their own, in the order the page draws them: what the workout
    /// was driven by first — a ride's power, cadence and speed; a run's power and its form.
    /// Recovery is one reading and is a figure, not a trace; a series of one sample is not a line.
    static func traced(_ workout: RecordedWorkout) -> [MetricReadout] {
        let order: [WorkoutMetric] = workout.activity.isRunning
            ? [.power, .speed, .groundContactTime, .verticalOscillation, .strideLength]
            : [.power, .cadence, .speed]
        return order.compactMap(workout.metric).filter { $0.samples.count >= 2 }
    }

    /// The form figures a run carries, in the order a coach reads them: the averages only.
    static func runningForm(_ workout: RecordedWorkout) -> [MetricReadout] {
        guard workout.activity.isRunning else { return [] }
        return [.power, .speed, .groundContactTime, .verticalOscillation, .strideLength].compactMap(workout.metric)
    }
}
