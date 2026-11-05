import Foundation

/// A recorded workout — a run, a ride, a hike, a walk, imported from Apple Health or the Strava
/// archive — read for its own page (`GET /api/training/sessions/{id}/recording`).
///
/// Every figure is the server's: the distance, time and climb are the session's own set, and the
/// splits, moving time and descent are derived from the route there, so the page, the records and
/// the chat model read one number each. The client converts a per-kilometre pace to per-mile, or to
/// a ride's speed, which is a change of unit and not a second derivation.
struct RecordedWorkout: Equatable, Sendable {
    let trainingSessionId: UUID
    let activity: RecordedActivity
    /// Where it was imported from, and who recorded it.
    let source: SessionSource
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
    /// What else the recording app measured, one entry per metric; empty for none.
    let metrics: [MetricReadout]

    /// One metric's readout, where the app wrote it.
    func metric(_ metric: WorkoutMetric) -> MetricReadout? {
        metrics.first { $0.metric == metric }
    }

    var elapsed: TimeInterval { end.timeIntervalSince(start) }

    /// The app that recorded it, as the page names it; empty where the source does not say.
    var recordedBy: String { source.recordedBy ?? "" }
}

/// What a recorded workout is: a run along a route, or an endurance activity.
enum RecordedActivity: Equatable, Sendable {
    case run
    case endurance(EnduranceActivity)

    init(wire: String?) {
        switch wire {
        case "run", nil: self = .run
        case let other?: self = .endurance(EnduranceActivity(wire: other))
        }
    }

    var title: String {
        switch self {
        case .run: "Run"
        case .endurance(let activity): activity.title
        }
    }

    /// Whether the page reads it as a speed rather than a pace: a ride.
    var readsAsSpeed: Bool {
        switch self {
        case .run: false
        case .endurance(let activity): activity.readsAsSpeed
        }
    }

    /// Whether running form is what its metrics describe: a run, indoors or out.
    var isRunning: Bool {
        switch self {
        case .run, .endurance(.indoorRun): true
        case .endurance: false
        }
    }
}

/// What one of a workout's metric series measures, each in the one unit the server holds it in.
enum WorkoutMetric: Hashable, Sendable {
    /// Watts.
    case power
    /// Pedal revolutions per minute.
    case cadence
    /// Metres per second.
    case speed
    /// Milliseconds.
    case groundContactTime
    /// Centimetres.
    case verticalOscillation
    /// Metres.
    case strideLength
    /// Beats per minute the heart rate fell in the minute after the workout.
    case heartRateRecovery
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "power": self = .power
        case "cadence": self = .cadence
        case "speed": self = .speed
        case "groundContactTime": self = .groundContactTime
        case "verticalOscillation": self = .verticalOscillation
        case "strideLength": self = .strideLength
        case "heartRateRecovery": self = .heartRateRecovery
        default: self = .unrecognised(wire)
        }
    }

    var wire: String {
        switch self {
        case .power: "power"
        case .cadence: "cadence"
        case .speed: "speed"
        case .groundContactTime: "groundContactTime"
        case .verticalOscillation: "verticalOscillation"
        case .strideLength: "strideLength"
        case .heartRateRecovery: "heartRateRecovery"
        case .unrecognised(let wire): wire
        }
    }
}

/// One metric across a workout: its mean over the samples, its highest, and the samples to draw.
struct MetricReadout: Equatable, Sendable {
    let metric: WorkoutMetric
    let average: Double
    let peak: Double
    let samples: [MetricSample]
}

struct MetricSample: Equatable, Sendable {
    let at: Date
    let value: Double
}

/// The heart-rate samples across a workout and whose they are.
struct HeartRateTrace: Equatable, Sendable {
    let source: HeartRateSource
    let samples: [HeartRateSample]
}

/// One stretch of a workout a unit long — a kilometre or a mile — or what is left after the last whole one.
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
                codingPath: decoder.codingPath, debugDescription: "A workout needs its session and its clock time."))
        }
        self.init(
            trainingSessionId: id,
            activity: RecordedActivity(wire: container.lenient(String.self, "activity")),
            source: container.lenient(SessionSource.self, "source") ?? .unrecognised(""),
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
            route: container.lenientList(RouteCoordinate.self, "route"),
            metrics: container.lenientList(MetricReadout.self, "metrics"))
    }
}

extension MetricReadout: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let metric = container.lenient(String.self, "metric"),
              let average = container.lenient(Double.self, "average")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A metric needs its name and its mean."))
        }
        self.init(
            metric: WorkoutMetric(wire: metric),
            average: average,
            peak: container.lenient("peak", or: average),
            samples: container.lenientList(MetricSample.self, "samples"))
    }
}

extension MetricSample: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let at = container.instant("at"), let value = container.lenient(Double.self, "value") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A reading needs its instant and its value."))
        }
        self.init(at: at, value: value)
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
