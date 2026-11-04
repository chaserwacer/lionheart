import Foundation

/// The units every figure is read in: pounds, miles, feet, inches and degrees Fahrenheit.
///
/// The athlete reads imperial, so every figure a screen shows passes through here on its way to a
/// string. Stored and wire values keep the unit their source measured or logged them in — Oura and
/// Health in metres, metres a second and °C, a set in the unit it was logged in — so nothing he
/// recorded changes when a reading does. The server's `Imperial` is the same rule for the text the
/// chat model repeats to him.
///
/// A distance logged in metres or yards reads as logged: on a track a 400 is a 400, not a quarter
/// of a mile. Kilometres read as miles.
enum Imperial {
    static let metersPerMile = 1609.344
    static let metersPerFoot = 0.3048
    static let metersPerInch = 0.0254
    static let kilogramsPerPound = 0.45359237

    // MARK: - Conversions

    static func miles(meters: Double) -> Double { meters / metersPerMile }

    static func feet(meters: Double) -> Double { meters / metersPerFoot }

    static func inches(meters: Double) -> Double { meters / metersPerInch }

    static func milesPerHour(metersPerSecond: Double) -> Double { metersPerSecond * 3600 / metersPerMile }

    /// A time per kilometre as the time per mile.
    static func perMile(perKilometre seconds: TimeInterval) -> TimeInterval { seconds * metersPerMile / 1000 }

    /// A weight in pounds, whichever unit it was logged in. A unit this build cannot name is left
    /// as it arrived, since converting it would be a guess.
    static func pounds(_ weight: Double, from unit: WeightUnit) -> Double {
        switch unit {
        case .pounds, .unrecognised: weight
        case .kilograms: weight / kilogramsPerPound
        }
    }

    /// A temperature *change* in Fahrenheit: the scale's 9/5 and no offset, since a deviation of
    /// nothing is nothing in either scale.
    static func fahrenheitChange(celsius: Double) -> Double { celsius * 9 / 5 }

    /// A logged distance in the unit it reads in: kilometres as miles, the rest as logged.
    static func distance(_ value: Double, _ unit: DistanceUnit) -> (value: Double, unit: DistanceUnit) {
        switch unit {
        case .kilometers: (miles(meters: value * 1000), .miles)
        case .meters, .yards, .miles, .unrecognised: (value, unit)
        }
    }

    // MARK: - Text

    /// A distance in metres as miles, to two places: "6.34 mi".
    static func distance(meters: Double) -> String {
        String(format: "%.2f mi", miles(meters: meters))
    }

    /// A distance the ring measured by walking: feet under a tenth of a mile, where "0.0 mi" would
    /// read as no distance at all, and miles to a tenth above it.
    static func roughDistance(meters: Double) -> String {
        let miles = miles(meters: meters)
        return miles < 0.1
            ? "\(Int(feet(meters: meters).rounded())) ft"
            : "\(MeasurementText.plain((miles * 10).rounded() / 10)) mi"
    }

    /// A climb or a descent in whole feet: "459 ft".
    static func climb(meters: Double) -> String {
        "\(Int(feet(meters: meters).rounded())) ft"
    }

    /// A pace given per kilometre, as time per mile: "8:15 /mi".
    static func pace(perKilometre seconds: TimeInterval) -> String {
        pace(perMile: perMile(perKilometre: seconds))
    }

    static func pace(perMile seconds: TimeInterval) -> String {
        "\(MeasurementText.clock(seconds: seconds.rounded())) /mi"
    }

    /// A pace given per kilometre, as miles an hour: "17.7 mph".
    static func speed(perKilometre seconds: TimeInterval) -> String {
        guard seconds > 0 else { return "–" }
        return speed(metersPerSecond: 1000 / seconds)
    }

    /// A speed in metres per second as miles an hour, to a tenth.
    static func speed(metersPerSecond: Double) -> String {
        String(format: "%.1f mph", milesPerHour(metersPerSecond: metersPerSecond))
    }

    // MARK: - Splits

    /// A recorded workout's splits are read by the mile.
    static func splits(of workout: RecordedWorkout) -> [WorkoutSplit] { workout.miles }

    /// A mile split's own pace, which the server gives per mile: "8:07".
    static func splitPace(_ split: WorkoutSplit) -> String {
        MeasurementText.clock(seconds: split.pace.rounded())
    }

    /// A mile split's speed in mph, from its pace per mile, without the unit: "17.7".
    static func splitSpeed(_ split: WorkoutSplit) -> String {
        guard split.pace > 0 else { return "–" }
        return String(format: "%.1f", 3600 / split.pace)
    }
}
