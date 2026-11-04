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

    /// A value of the metric in its unit. A run's speed reads as a pace, a ride's as a speed, in the
    /// page's unit; stride length stays in metres, which is how every watch states it.
    static func figure(_ metric: WorkoutMetric, _ value: Double, for activity: RecordedActivity, in unit: RunUnit) -> String {
        switch metric {
        case .power: "\(Int(value.rounded())) W"
        case .cadence: "\(Int(value.rounded())) rpm"
        case .speed:
            activity.isRunning && value > 0
                ? unit.pace(perKilometre: 1000 / value)
                : unit.speed(metersPerSecond: value)
        case .groundContactTime: "\(Int(value.rounded())) ms"
        case .verticalOscillation: String(format: "%.1f cm", value)
        case .strideLength: String(format: "%.2f m", value)
        case .heartRateRecovery: "\(Int(value.rounded())) bpm"
        case .unrecognised: String(format: "%.1f", value)
        }
    }

    /// The axis label for a trace of the metric: its unit alone.
    static func unitLabel(_ metric: WorkoutMetric, in unit: RunUnit) -> String {
        switch metric {
        case .power: "W"
        case .cadence: "rpm"
        case .speed: unit.speedAbbreviation
        case .groundContactTime: "ms"
        case .verticalOscillation: "cm"
        case .strideLength: "m"
        case .heartRateRecovery: "bpm"
        case .unrecognised: ""
        }
    }

    /// The value a trace plots: a speed in the page's unit an hour, everything else as held.
    static func plotted(_ metric: WorkoutMetric, _ value: Double, in unit: RunUnit) -> Double {
        switch metric {
        case .speed: value * 3600 / unit.meters
        case .power, .cadence, .groundContactTime, .verticalOscillation, .strideLength, .heartRateRecovery, .unrecognised:
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
