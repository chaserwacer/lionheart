import Foundation

/// The athlete's logged bodyweights as one daily series, and the questions other screens ask of it.
///
/// The question that matters most is "what did he weigh on that day", because a weighted dip is
/// judged by the load he moved — bodyweight plus the plate — and the plate alone cannot be compared
/// across months in which the bodyweight moved. A day with no measurement takes the newest one
/// before it: bodyweight drifts over weeks, so the last reading is a far better answer than none,
/// and the reading's own day travels with it so a screen can say how old it is.
struct BodyweightSeries: Equatable, Sendable {
    /// Oldest first, one per day.
    let measurements: [BodyweightMeasurement]

    init(_ measurements: [BodyweightMeasurement]) {
        // Uniqued by day, newest answer kept, in case two reads were merged.
        let byDay = Dictionary(measurements.map { ($0.day, $0) }, uniquingKeysWith: { _, later in later })
        self.measurements = byDay.values.sorted { $0.day < $1.day }
    }

    var latest: BodyweightMeasurement? { measurements.last }
    var isEmpty: Bool { measurements.isEmpty }

    /// The measurement in force on `day`: the newest one on or before it.
    func inForce(on day: CalendarDay) -> BodyweightMeasurement? {
        measurements.last { $0.day <= day }
    }

    /// The series in one unit, as chart points. Weights never convert, so a reading in another unit
    /// is left out rather than turned into a number the athlete never stood on the scale for.
    func points(in unit: WeightUnit, from: CalendarDay? = nil) -> [MetricPoint] {
        measurements
            .filter { measurement in
                measurement.unit == unit && (from.map { measurement.day >= $0 } ?? true)
            }
            .map { MetricPoint(day: $0.day, value: $0.weight) }
    }

    /// How far the latest reading sits from the one in force `days` before it, in the latest's
    /// unit; `nil` with nothing that old to compare against.
    func change(over days: Int, calendar: Calendar = .current) -> Double? {
        guard let latest else { return nil }
        guard let then = inForce(on: latest.day.adding(days: -days, in: calendar)), then.unit == latest.unit,
              then.day != latest.day
        else { return nil }
        return latest.weight - then.weight
    }
}
