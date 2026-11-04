import Foundation

/// Lionheart's own training score for the seven days ending on a day, part by part — the server's
/// `TrainingScore` (`GET /api/training/score/{day}`). Not Oura's activity score.
///
/// Every part arrives with the measurements it was read from, so the screen states measurements
/// ("push 6 of 8 hard sets") rather than sub-scores. A part with no data is in `unknown`: its points
/// are out of reach rather than counted as zero, so `outOf` is 100 less its weight.
struct TrainingScore: Equatable, Sendable {
    let window: DayRange
    let value: Int
    let outOf: Int
    let unknown: [ScorePart]
    let lifting: LiftingPart
    let cardio: CardioPart
    let progress: ProgressPart
    let consistency: ConsistencyPart

    var isPartial: Bool { !unknown.isEmpty }
}

enum ScorePart: Hashable, Sendable {
    case lifting, cardio, progress, consistency
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "lifting": self = .lifting
        case "cardio": self = .cardio
        case "progress": self = .progress
        case "consistency": self = .consistency
        default: self = .unrecognised(wire)
        }
    }

    var name: String {
        switch self {
        case .lifting: "Lifting"
        case .cardio: "Cardio"
        case .progress: "Progress"
        case .consistency: "Consistency"
        case .unrecognised(let wire): wire
        }
    }
}

/// One measurement of a part with the points it earned of the points it could — the server's
/// `Scored<T>`, so the breakdown draws the server's arithmetic rather than a second reading of it.
struct Scored<Value: Equatable & Sendable>: Equatable, Sendable {
    let of: Value
    let points: Double
    let outOf: Double

    /// The share of its points it earned, 0 to 1.
    var share: Double { outOf > 0 ? min(1, max(0, points / outOf)) : 0 }
}

/// Hard sets of push, pull and legs against eight each.
struct LiftingPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let families: [Scored<FamilyVolume>]
}

/// Zone-2 and zone 3–5 minutes; `points` is `nil` and `zones` empty when the measure is unknown.
struct CardioPart: Equatable, Sendable {
    let points: Double?
    let outOf: Int
    let measure: CardioMeasure
    let zones: [Scored<ZoneTarget>]
}

/// Minutes in a band of heart-rate zones against the week's target for it.
struct ZoneTarget: Equatable, Sendable {
    let band: ZoneBand
    let minutes: Double
    let target: Int
}

enum ZoneBand: Equatable, Sendable {
    /// 60–70 % of maximum heart rate.
    case zone2
    /// 70 % of maximum and above.
    case zones3To5
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "zone2": self = .zone2
        case "zones3To5": self = .zones3To5
        default: self = .unrecognised(wire)
        }
    }

    var name: String {
        switch self {
        case .zone2: "Zone 2"
        case .zones3To5: "Zones 3–5"
        case .unrecognised(let wire): wire
        }
    }
}

/// Each family's priority lift against its twelve weeks before.
struct ProgressPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let families: [Scored<FamilyPriority>]
}

/// Sessions in the window and each of the three seven-day blocks before it, newest first.
struct ConsistencyPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let blocks: [Scored<SessionBlock>]
    let target: Int
}

/// Completed sessions in one seven-day block.
struct SessionBlock: Equatable, Sendable {
    let window: DayRange
    let sessions: Int
}

/// One week's score, for the trend.
struct WeeklyScore: Equatable, Sendable, Identifiable {
    let window: DayRange
    let value: Int
    let outOf: Int
    let unknown: [ScorePart]
    let liftingPoints: Double
    let cardioPoints: Double?
    let progressPoints: Double
    let consistencyPoints: Double

    var id: CalendarDay { window.to }
    var isPartial: Bool { !unknown.isEmpty }

    /// The week's points from one part, or its whole score when `part` is `nil`; `nil` when that
    /// part was unknown.
    func points(of part: ScorePart?) -> Double? {
        switch part {
        case nil: Double(value)
        case .lifting?: liftingPoints
        case .cardio?: cardioPoints
        case .progress?: progressPoints
        case .consistency?: consistencyPoints
        case .unrecognised?: nil
        }
    }
}

// MARK: - Decoding

extension TrainingScore: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window"),
              let value = container.lenient(Int.self, "value")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A training score needs its window and value."))
        }
        self.init(
            window: window,
            value: value,
            outOf: container.lenient("outOf", or: 100),
            unknown: container.lenientList(String.self, "unknown").map(ScorePart.init(wire:)),
            lifting: container.lenient(LiftingPart.self, "lifting") ?? LiftingPart(points: 0, outOf: 0, families: []),
            cardio: container.lenient(CardioPart.self, "cardio")
                ?? CardioPart(points: nil, outOf: 0, measure: .unrecognised(""), zones: []),
            progress: container.lenient(ProgressPart.self, "progress") ?? ProgressPart(points: 0, outOf: 0, families: []),
            consistency: container.lenient(ConsistencyPart.self, "consistency")
                ?? ConsistencyPart(points: 0, outOf: 0, blocks: [], target: 0))
    }
}

extension LiftingPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            families: container.lenientList(Scored<FamilyVolume>.self, "families"))
    }
}

extension CardioPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient(Double.self, "points"),
            outOf: container.lenient("outOf", or: 0),
            measure: container.lenient(CardioMeasure.self, "measure") ?? .unrecognised(""),
            zones: container.lenientList(Scored<ZoneTarget>.self, "zones"))
    }
}

extension ProgressPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            families: container.lenientList(Scored<FamilyPriority>.self, "families"))
    }
}

extension ConsistencyPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            blocks: container.lenientList(Scored<SessionBlock>.self, "blocks"),
            target: container.lenient("target", or: 0))
    }
}

extension Scored: Decodable where Value: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let of = container.lenient(Value.self, "of") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A scored measurement needs what it measured."))
        }
        self.init(of: of, points: container.lenient("points", or: 0), outOf: container.lenient("outOf", or: 0))
    }
}

extension ZoneTarget: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            band: ZoneBand(wire: container.lenient("band", or: "")),
            minutes: container.lenient("minutes", or: 0),
            target: container.lenient("target", or: 0))
    }
}

extension SessionBlock: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A block of sessions needs its window."))
        }
        self.init(window: window, sessions: container.lenient("sessions", or: 0))
    }
}

extension WeeklyScore: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window"),
              let value = container.lenient(Int.self, "value")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A weekly score needs its window and value."))
        }
        self.init(
            window: window,
            value: value,
            outOf: container.lenient("outOf", or: 100),
            unknown: container.lenientList(String.self, "unknown").map(ScorePart.init(wire:)),
            liftingPoints: container.lenient("liftingPoints", or: 0),
            cardioPoints: container.lenient(Double.self, "cardioPoints"),
            progressPoints: container.lenient("progressPoints", or: 0),
            consistencyPoints: container.lenient("consistencyPoints", or: 0))
    }
}
