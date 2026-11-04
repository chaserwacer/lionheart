import Foundation

/// The unit a recorded workout's page reads distance, pace and speed in: kilometres or miles.
///
/// The server sends paces per kilometre and splits in both units; reading a page in miles converts
/// a pace by the length of a mile, and a ride's speed is the pace inverted — changes of unit, not a
/// second derivation.
enum RunUnit: String, CaseIterable, Sendable {
    case kilometres
    case miles

    var meters: Double {
        switch self {
        case .kilometres: 1000
        case .miles: 1609.344
        }
    }

    var abbreviation: String {
        switch self {
        case .kilometres: "km"
        case .miles: "mi"
        }
    }

    /// A distance in metres, in this unit, to two places: "10.21 km".
    func distance(_ meters: Double) -> String {
        String(format: "%.2f %@", meters / self.meters, abbreviation)
    }

    /// A pace given per kilometre, as time per this unit: "5:07 /km", "8:15 /mi".
    func pace(perKilometre seconds: TimeInterval) -> String {
        "\(MeasurementText.clock(seconds: (seconds * meters / 1000).rounded())) /\(abbreviation)"
    }

    /// A pace given per kilometre, as distance an hour in this unit: "28.4 km/h", "17.7 mph".
    func speed(perKilometre seconds: TimeInterval) -> String {
        guard seconds > 0 else { return "–" }
        return speed(metersPerSecond: 1000 / seconds)
    }

    /// A speed in metres per second, in this unit an hour, to a tenth.
    func speed(metersPerSecond: Double) -> String {
        String(format: "%.1f %@", metersPerSecond * 3600 / meters, speedAbbreviation)
    }

    var speedAbbreviation: String {
        switch self {
        case .kilometres: "km/h"
        case .miles: "mph"
        }
    }

    /// A split's own pace, which the server gives per the split's unit: "5:07".
    static func splitPace(_ split: WorkoutSplit) -> String {
        MeasurementText.clock(seconds: split.pace.rounded())
    }

    /// A split's speed, from its pace per the split's unit, without the unit: "28.4".
    func splitSpeed(_ split: WorkoutSplit) -> String {
        guard split.pace > 0 else { return "–" }
        return String(format: "%.1f", 3600 / split.pace)
    }

    /// The workout's splits in this unit.
    func splits(of run: RecordedWorkout) -> [WorkoutSplit] {
        switch self {
        case .kilometres: run.kilometres
        case .miles: run.miles
        }
    }
}
