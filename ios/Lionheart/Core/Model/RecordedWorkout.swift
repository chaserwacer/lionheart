import Foundation

/// A run imported from Apple Health, read for its own page (`GET /api/training/sessions/{id}/recording`).
///
/// Every figure is the server's: the distance, time and climb are the session's own set, and the
/// splits, moving time and descent are derived from the route there, so the page, the records and
/// the chat model read one number each. The client converts a per-kilometre pace to per-mile, which
/// is a change of unit and not a second derivation.
struct RecordedWorkout: Equatable, Sendable {
    let trainingSessionId: UUID
    /// The app that wrote the run to Health, as Health names it.
    let recordedBy: String
    let start: Date
    let end: Date
    let distanceMeters: Double
    /// The time the recording app counted, which excludes a pause it recorded.
    let duration: TimeInterval
    /// Time in motion along the route; `nil` with no route to read it from.
    let movingTime: TimeInterval?
    /// Seconds per kilometre over the duration; `nil` with no distance.
    let pace: TimeInterval?
    /// Seconds per kilometre over the moving time.
    let movingPace: TimeInterval?
    let climbMeters: Double
    /// `nil` where the route carried no altitude.
    let descentMeters: Double?
    /// The cardio report's own reading of the run's heart rate.
    let heartRate: SessionHeartRate
    let maxHeartRate: MaxHeartRate?
    let trace: HeartRateTrace
    let kilometres: [WorkoutSplit]
    let miles: [WorkoutSplit]
    /// The path, oldest fix first. Where the athlete was: drawn on his own phone and nowhere else.
    let route: [RouteCoordinate]

    var elapsed: TimeInterval { end.timeIntervalSince(start) }
}

/// The heart-rate samples across a run and whose they are.
struct HeartRateTrace: Equatable, Sendable {
    let source: HeartRateSource
    let samples: [HeartRateSample]
}

/// One stretch of a run a unit long — a kilometre or a mile — or what is left after the last whole one.
struct WorkoutSplit: Equatable, Sendable, Identifiable {
    let number: Int
    let distanceMeters: Double
    /// Clock time across the split, a stop included.
    let elapsed: TimeInterval
    let moving: TimeInterval
    /// Moving time per whole unit, so a short last split reads at the pace it was run.
    let pace: TimeInterval
    let climbMeters: Double?
    let descentMeters: Double?
    let averageBpm: Int?

    var id: Int { number }
}

/// A place on a route.
struct RouteCoordinate: Equatable, Sendable {
    let latitude: Double
    let longitude: Double
}

// MARK: - Decoding

extension RecordedWorkout: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "trainingSessionId"),
              let start = container.instant("start"),
              let end = container.instant("end")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A run needs its session and its clock time."))
        }
        let recordedBy = container.lenient(Named.self, "recordedBy")
        self.init(
            trainingSessionId: id,
            recordedBy: recordedBy?.name ?? "",
            start: start,
            end: end,
            distanceMeters: container.lenient("distanceMeters", or: 0),
            duration: container.duration("duration") ?? end.timeIntervalSince(start),
            movingTime: container.duration("movingTime"),
            pace: container.duration("pace"),
            movingPace: container.duration("movingPace"),
            climbMeters: container.lenient("climbMeters", or: 0),
            descentMeters: container.lenient(Double.self, "descentMeters"),
            heartRate: container.lenient(SessionHeartRate.self, "heartRate") ?? .notRecorded,
            maxHeartRate: container.lenient(MaxHeartRate.self, "maxHeartRate"),
            trace: container.lenient(HeartRateTrace.self, "trace") ?? HeartRateTrace(source: .ring, samples: []),
            kilometres: container.lenientList(WorkoutSplit.self, "kilometres"),
            miles: container.lenientList(WorkoutSplit.self, "miles"),
            route: container.lenientList(RouteCoordinate.self, "route"))
    }

    private struct Named: Decodable {
        let name: String
    }
}

extension HeartRateTrace: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            source: HeartRateSource(wire: container.lenient(String.self, "source")),
            samples: container.lenientList(HeartRateSample.self, "samples"))
    }
}

extension WorkoutSplit: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let number = container.lenient(Int.self, "number"),
              let distance = container.lenient(Double.self, "distanceMeters"),
              let pace = container.duration("pace")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A split needs its number, distance and pace."))
        }
        self.init(
            number: number,
            distanceMeters: distance,
            elapsed: container.duration("elapsed") ?? 0,
            moving: container.duration("moving") ?? 0,
            pace: pace,
            climbMeters: container.lenient(Double.self, "climbMeters"),
            descentMeters: container.lenient(Double.self, "descentMeters"),
            averageBpm: container.lenient(Int.self, "averageBpm"))
    }
}

extension RouteCoordinate: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let latitude = container.lenient(Double.self, "latitude"),
              let longitude = container.lenient(Double.self, "longitude")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A coordinate needs both halves."))
        }
        self.init(latitude: latitude, longitude: longitude)
    }
}
