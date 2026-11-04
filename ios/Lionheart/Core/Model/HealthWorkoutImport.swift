import Foundation

/// One Apple Health workout as the phone read it, offered to the server to import.
///
/// The phone sends what Health holds and decides nothing — which workouts become training is the
/// server's rule, written once. A plain value, so `Health/` builds it from HealthKit's types and
/// nothing past that layer ever sees one of them.
struct HealthWorkoutImport: Equatable, Sendable {
    /// The workout's UUID in Health, which a second import of it is recognised by.
    let workoutId: UUID
    /// Health's activity in its own spelling: "running", "cycling", "hiking", "walking".
    let activity: String
    let indoor: Bool
    let appName: String
    let appBundleId: String
    let start: Date
    let end: Date
    /// The zone the workout was recorded in, so its day is the day the athlete lived it.
    let timeZone: TimeZone
    /// The time the recording app counted, which excludes a pause it recorded.
    let duration: TimeInterval
    let distanceMeters: Double?
    let elevationAscendedMeters: Double?
    /// Oldest first.
    let route: [RouteFix]
    /// The heart rate the same app wrote across the workout, oldest first.
    let heartRate: [HeartRateReading]
    /// Everything else the same app measured, one series per metric — power, cadence, speed,
    /// running form, and the heart rate's recovery after it.
    let metrics: [MetricSeries]

    /// One fix of the route.
    struct RouteFix: Equatable, Sendable {
        let at: Date
        let latitude: Double
        let longitude: Double
        /// `nil` where the fix carried no valid altitude.
        let altitude: Double?
    }

    struct HeartRateReading: Equatable, Sendable {
        let at: Date
        let bpm: Int
    }

    /// One metric's samples, each in the unit the server holds the metric in (`WorkoutMetric`).
    struct MetricSeries: Equatable, Sendable {
        let metric: WorkoutMetric
        let samples: [MetricSample]
    }
}

extension HealthWorkoutImport: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(workoutId, forKey: WireKey(stringValue: "workoutId"))
        try container.encode(activity, forKey: WireKey(stringValue: "activity"))
        try container.encode(indoor, forKey: WireKey(stringValue: "indoor"))
        try container.encode(appName, forKey: WireKey(stringValue: "appName"))
        try container.encode(appBundleId, forKey: WireKey(stringValue: "appBundleId"))
        // At the zone the run was recorded in, because the server reads the session's day at the
        // offset it is given (see `WireDate.offsetWire`).
        try container.encode(WireDate.offsetWire(from: start, in: timeZone), forKey: WireKey(stringValue: "start"))
        try container.encode(WireDate.offsetWire(from: end, in: timeZone), forKey: WireKey(stringValue: "end"))
        try container.encode(WireDuration.wire(fromSeconds: duration), forKey: WireKey(stringValue: "duration"))
        try container.encode(distanceMeters, forKey: WireKey(stringValue: "distanceMeters"))
        try container.encode(elevationAscendedMeters, forKey: WireKey(stringValue: "elevationAscendedMeters"))
        try container.encode(route, forKey: WireKey(stringValue: "route"))
        try container.encode(heartRate, forKey: WireKey(stringValue: "heartRate"))
        try container.encode(metrics, forKey: WireKey(stringValue: "metrics"))
    }
}

extension HealthWorkoutImport.MetricSeries: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(metric.wire, forKey: WireKey(stringValue: "metric"))
        try container.encode(samples.map(Sample.init), forKey: WireKey(stringValue: "samples"))
    }

    private struct Sample: Encodable {
        let reading: MetricSample

        init(_ reading: MetricSample) { self.reading = reading }

        func encode(to encoder: any Encoder) throws {
            var container = encoder.container(keyedBy: WireKey.self)
            try container.encode(WireDate.wire(from: reading.at), forKey: WireKey(stringValue: "at"))
            try container.encode(reading.value, forKey: WireKey(stringValue: "value"))
        }
    }
}

extension HealthWorkoutImport.RouteFix: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(WireDate.wire(from: at), forKey: WireKey(stringValue: "at"))
        try container.encode(latitude, forKey: WireKey(stringValue: "latitude"))
        try container.encode(longitude, forKey: WireKey(stringValue: "longitude"))
        try container.encode(altitude, forKey: WireKey(stringValue: "altitude"))
    }
}

extension HealthWorkoutImport.HeartRateReading: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(WireDate.wire(from: at), forKey: WireKey(stringValue: "at"))
        try container.encode(bpm, forKey: WireKey(stringValue: "bpm"))
    }
}
