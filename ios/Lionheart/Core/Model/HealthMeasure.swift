import Foundation

/// A figure Apple's own devices measure and write to Health that Lionheart keeps day by day.
///
/// Each is held in one fixed unit, the server's: ml/kg/min, bpm, ms, breaths/min, m/s, m, a fraction
/// for the three shares, m for the walk-test distance. The phone reads Health in exactly these.
///
/// **HRV here is SDNN** and Oura's is RMSSD: different statistics over different windows, so the
/// two are never drawn on one axis or compared.
enum HealthMetric: String, CaseIterable, Hashable, Sendable {
    case vo2Max
    case restingHeartRate
    case heartRateVariabilitySdnn
    case respiratoryRate
    case walkingHeartRateAverage
    case walkingSpeed
    case walkingStepLength
    case walkingDoubleSupport
    case walkingAsymmetry
    case walkingSteadiness
    case sixMinuteWalkDistance

    /// The watch's figures: the fitness and recovery history.
    static let watchFigures: [HealthMetric] = [
        .vo2Max, .restingHeartRate, .heartRateVariabilitySdnn, .respiratoryRate, .walkingHeartRateAverage,
    ]

    /// The phone's gait figures, in the order the Body section lists them.
    static let gaitFigures: [HealthMetric] = [
        .walkingSpeed, .walkingStepLength, .walkingAsymmetry, .walkingDoubleSupport, .walkingSteadiness,
        .sixMinuteWalkDistance,
    ]
}

/// Which of Apple's devices took a reading.
enum MeasuringDevice: String, CaseIterable, Hashable, Sendable {
    case appleWatch
    case iPhone
}

/// One day of one figure: the mean of its readings, their extremes, and how many there were.
struct MeasuredDay: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let mean: Double
    let minimum: Double
    let maximum: Double
    let count: Int

    var id: CalendarDay { day }
}

/// One figure as one device measured it, oldest day first.
struct HealthMeasureSeries: Equatable, Sendable {
    let metric: HealthMetric
    let device: MeasuringDevice
    let days: [MeasuredDay]
}

/// A day of a figure as the phone offers it to the server.
struct MeasuredDayOffer: Equatable, Sendable {
    let metric: HealthMetric
    let device: MeasuringDevice
    let measured: MeasuredDay
}

/// One weigh-in Health holds, offered as a bodyweight. Kilograms, Health's own unit for mass; the
/// server states it in the athlete's unit.
struct WeighIn: Equatable, Sendable {
    let day: CalendarDay
    let at: Date
    let kilograms: Double
}

/// What a batch of days did on the server.
struct HealthMeasuresImportReport: Equatable, Sendable {
    let recorded: Int
    let restated: Int
    let unchanged: Int
    let unreadable: Int

    static let empty = HealthMeasuresImportReport(recorded: 0, restated: 0, unchanged: 0, unreadable: 0)
}

/// What a batch of weigh-ins did to the athlete's bodyweight.
struct HealthBodyweightImportReport: Equatable, Sendable {
    /// Days that had no bodyweight and now have the day's first weigh-in.
    let added: Int
    let daysAlreadyMeasured: Int
    let unreadable: Int
    /// Weighted dips and pull-ups whose system load now reads a different bodyweight.
    let setsReweighed: Int
}

/// The profile's age after Health's date of birth was read.
struct ProfileAge: Equatable, Sendable {
    let age: Int
    let changed: Bool
}

// MARK: - Wire

extension HealthMeasureSeries: Decodable {
    /// A series of a figure or device this build does not know is malformed for it, so it throws
    /// and the list it sits in drops it: nothing on screen could draw it.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let metric = container.lenient(String.self, "metric").flatMap(HealthMetric.init(rawValue:)),
              let device = container.lenient(String.self, "device").flatMap(MeasuringDevice.init(rawValue:))
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A series names a figure and a device this build knows."))
        }
        self.init(metric: metric, device: device, days: container.lenientList(MeasuredDay.self, "days"))
    }
}

extension MeasuredDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day"), let mean = container.lenient(Double.self, "mean") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A measured day needs its day and mean."))
        }
        self.init(
            day: day,
            mean: mean,
            minimum: container.lenient("minimum", or: mean),
            maximum: container.lenient("maximum", or: mean),
            count: container.lenient("count", or: 1))
    }
}

extension MeasuredDayOffer: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(metric.rawValue, forKey: WireKey(stringValue: "metric"))
        try container.encode(device.rawValue, forKey: WireKey(stringValue: "device"))
        try container.encode(measured.day.wire, forKey: WireKey(stringValue: "day"))
        try container.encode(measured.mean, forKey: WireKey(stringValue: "mean"))
        try container.encode(measured.minimum, forKey: WireKey(stringValue: "minimum"))
        try container.encode(measured.maximum, forKey: WireKey(stringValue: "maximum"))
        try container.encode(measured.count, forKey: WireKey(stringValue: "count"))
    }
}

extension WeighIn: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(day.wire, forKey: WireKey(stringValue: "day"))
        try container.encode(WireDate.wire(from: at), forKey: WireKey(stringValue: "at"))
        try container.encode(kilograms, forKey: WireKey(stringValue: "kilograms"))
    }
}

extension HealthMeasuresImportReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            recorded: container.lenient("recorded", or: 0),
            restated: container.lenient("restated", or: 0),
            unchanged: container.lenient("unchanged", or: 0),
            unreadable: container.lenient("unreadable", or: 0))
    }
}

extension HealthBodyweightImportReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            added: container.lenient("added", or: 0),
            daysAlreadyMeasured: container.lenient("daysAlreadyMeasured", or: 0),
            unreadable: container.lenient("unreadable", or: 0),
            setsReweighed: container.lenient("setsReweighed", or: 0))
    }
}

extension ProfileAge: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let age = container.lenient(Int.self, "age") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "An age."))
        }
        self.init(age: age, changed: container.lenient("changed", or: false))
    }
}
