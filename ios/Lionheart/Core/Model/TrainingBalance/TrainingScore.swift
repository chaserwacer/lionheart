import Foundation

/// Lionheart's own training score for the seven days ending on a day, part by part — the server's
/// `TrainingScore` (`GET /api/training/score/{day}`). Not Oura's activity score.
///
/// Every part arrives with the measurements it was read from, so the screen states measurements
/// ("push 6 of 8 hard sets") rather than sub-scores. A part with no data is in `unknown`, and the
/// value is then read over the parts that are known and never reaches 90.
struct TrainingScore: Equatable, Sendable {
    let window: DayRange
    let value: Int
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

/// Hard sets of push, pull and legs against eight each.
struct LiftingPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let families: [FamilyVolume]
}

/// Zone-2 and high-intensity minutes; `points` is `nil` when the measure is unknown.
struct CardioPart: Equatable, Sendable {
    let points: Double?
    let outOf: Int
    let measure: CardioMeasure
}

/// Each family's priority lift against its twelve weeks before.
struct ProgressPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let families: [FamilyPriority]
}

/// Sessions in the window and each of the three seven-day blocks before it, newest first.
struct ConsistencyPart: Equatable, Sendable {
    let points: Double
    let outOf: Int
    let sessionsPerBlock: [Int]
    let target: Int
}

/// One week's score, for the trend.
struct WeeklyScore: Equatable, Sendable, Identifiable {
    let window: DayRange
    let value: Int
    let unknown: [ScorePart]
    let liftingPoints: Double
    let cardioPoints: Double?
    let progressPoints: Double
    let consistencyPoints: Double

    var id: CalendarDay { window.to }
    var isPartial: Bool { !unknown.isEmpty }
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
            unknown: container.lenientList(String.self, "unknown").map(ScorePart.init(wire:)),
            lifting: container.lenient(LiftingPart.self, "lifting") ?? LiftingPart(points: 0, outOf: 0, families: []),
            cardio: container.lenient(CardioPart.self, "cardio")
                ?? CardioPart(points: nil, outOf: 0, measure: .unrecognised("")),
            progress: container.lenient(ProgressPart.self, "progress") ?? ProgressPart(points: 0, outOf: 0, families: []),
            consistency: container.lenient(ConsistencyPart.self, "consistency")
                ?? ConsistencyPart(points: 0, outOf: 0, sessionsPerBlock: [], target: 0))
    }
}

extension LiftingPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            families: container.lenientList(FamilyVolume.self, "families"))
    }
}

extension CardioPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient(Double.self, "points"),
            outOf: container.lenient("outOf", or: 0),
            measure: container.lenient(CardioMeasure.self, "measure") ?? .unrecognised(""))
    }
}

extension ProgressPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            families: container.lenientList(FamilyPriority.self, "families"))
    }
}

extension ConsistencyPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            points: container.lenient("points", or: 0),
            outOf: container.lenient("outOf", or: 0),
            sessionsPerBlock: container.lenientList(Int.self, "sessionsPerBlock"),
            target: container.lenient("target", or: 0))
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
            unknown: container.lenientList(String.self, "unknown").map(ScorePart.init(wire:)),
            liftingPoints: container.lenient("liftingPoints", or: 0),
            cardioPoints: container.lenient(Double.self, "cardioPoints"),
            progressPoints: container.lenient("progressPoints", or: 0),
            consistencyPoints: container.lenient("consistencyPoints", or: 0))
    }
}
