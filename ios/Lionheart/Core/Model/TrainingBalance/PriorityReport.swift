import Foundation

/// What the athlete is striving for over the seven days ending on a day: each family's priority lift
/// and the week's cardio — the server's `PriorityReport` (`GET /api/training/priorities/{day}`).
///
/// Read on the server rather than here because the score reads the same progress, and bodyweight on
/// a day — what a weighted dip's system load is built on — is the server's rule.
struct PriorityReport: Equatable, Sendable {
    let window: DayRange
    let families: [FamilyPriority]
    let cardio: CardioMeasure

    func lift(for family: PatternFamily) -> PriorityLift {
        families.first { $0.family == family }?.lift ?? .none
    }
}

struct FamilyPriority: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let lift: PriorityLift

    var id: String { family.wire }
}

enum PriorityLift: Equatable, Sendable {
    /// Nothing pinned and no compound lift of the family in eight weeks.
    case none
    case chosen(ChosenPriority)
    case unrecognised(String)
}

struct ChosenPriority: Equatable, Sendable {
    let movementDataId: UUID
    let name: String
    let choice: PriorityChoice
    let progress: LiftProgress
    /// Sessions with the lift in the 28 days ending on the window's last day.
    let sessionsLastFourWeeks: Int
    /// Each session's best estimated single over the last twelve weeks and the window, oldest first.
    let series: [SessionBest]
}

enum PriorityChoice: Equatable, Sendable {
    /// Pinned as the family's priority for a horizon covering the day.
    case pinned(FocusPin)
    /// Nothing pinned: the family's most-trained compound lift of eight weeks stands in.
    case mostTrained(sessions: Int)
    case unrecognised(String)
}

/// The window's best estimated single against the twelve weeks before, on system load where the
/// lift's weight adds to bodyweight.
enum LiftProgress: Equatable, Sendable {
    case notTrained
    /// Trained, but no set of one to twelve reps at a load.
    case noEstimate
    /// Trained with nothing in the twelve weeks before to compare with.
    case firstBack(best: Double, unit: WeightUnit, onSystemLoad: Bool)
    case compared(
        best: Double, priorBest: Double, standing: ProgressStanding, unit: WeightUnit, onSystemLoad: Bool)
    case unrecognised(String)
}

/// Where a compared best stands against the twelve weeks before, as the server banded it.
enum ProgressStanding: Equatable, Sendable {
    /// Above every best before the window.
    case lifetimeBest
    /// Above the twelve weeks before.
    case newBest
    /// Within five per cent under the prior best.
    case held
    case below
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "lifetimeBest": self = .lifetimeBest
        case "newBest": self = .newBest
        case "held": self = .held
        case "below": self = .below
        default: self = .unrecognised(wire)
        }
    }
}

struct SessionBest: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let estimatedOneRepMax: Double
    let load: Double
    let reps: Int

    var id: CalendarDay { day }
}

/// The window's cardio as the heart-rate zones read it, or why it cannot be read.
enum CardioMeasure: Equatable, Sendable {
    /// Zone 2 is 60–70 % of maximum heart rate; quality minutes are work bouts at zone 4–5 in
    /// endurance sessions holding at least ten of them. `heartRateDays` counts the ring's days.
    case measured(
        zone2Minutes: Double, qualityMinutes: Double, qualitySessions: Int, sessions: Int, heartRateDays: Int)
    case unknown(CardioUnknownReason)
    case unrecognised(String)
}

enum CardioUnknownReason: Equatable, Sendable {
    case noHeartRate, noMaximumHeartRate, sessionsUnrecorded
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "noHeartRate": self = .noHeartRate
        case "noMaximumHeartRate": self = .noMaximumHeartRate
        case "sessionsUnrecorded": self = .sessionsUnrecorded
        default: self = .unrecognised(wire)
        }
    }
}

// MARK: - Decoding

extension PriorityReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A priority report needs its window."))
        }
        self.init(
            window: window,
            families: container.lenientList(FamilyPriority.self, "families"),
            cardio: container.lenient(CardioMeasure.self, "cardio") ?? .unrecognised(""))
    }
}

extension FamilyPriority: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            lift: container.lenient(PriorityLift.self, "lift") ?? .unrecognised(""))
    }
}

extension PriorityLift: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "none":
            self = .none
        case "chosen":
            guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
                self = .unrecognised(type)
                return
            }
            self = .chosen(ChosenPriority(
                movementDataId: movementDataId,
                name: container.lenient("name", or: ""),
                choice: container.lenient(PriorityChoice.self, "choice") ?? .unrecognised(""),
                progress: container.lenient(LiftProgress.self, "progress") ?? .unrecognised(""),
                sessionsLastFourWeeks: container.lenient("sessionsLastFourWeeks", or: 0),
                series: container.lenientList(SessionBest.self, "series")))
        default:
            self = .unrecognised(type)
        }
    }
}

extension PriorityChoice: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "pinned":
            guard let pin = container.lenient(FocusPin.self, "pin") else {
                self = .unrecognised(type)
                return
            }
            self = .pinned(pin)
        case "mostTrained":
            self = .mostTrained(sessions: container.lenient("sessions", or: 0))
        default:
            self = .unrecognised(type)
        }
    }
}

extension LiftProgress: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        let unit = WeightUnit(wire: container.lenient(Int.self, "unit"))
        let onSystemLoad = container.lenient("onSystemLoad", or: false)
        switch type {
        case "notTrained":
            self = .notTrained
        case "noEstimate":
            self = .noEstimate
        case "firstBack":
            guard let best = container.lenient(Double.self, "best") else {
                self = .unrecognised(type)
                return
            }
            self = .firstBack(best: best, unit: unit, onSystemLoad: onSystemLoad)
        case "compared":
            guard let best = container.lenient(Double.self, "best"),
                  let prior = container.lenient(Double.self, "priorBest")
            else {
                self = .unrecognised(type)
                return
            }
            self = .compared(
                best: best, priorBest: prior,
                standing: ProgressStanding(wire: container.lenient("standing", or: "")),
                unit: unit, onSystemLoad: onSystemLoad)
        default:
            self = .unrecognised(type)
        }
    }
}

extension SessionBest: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day"), let estimate = container.lenient(Double.self, "estimatedOneRepMax") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A session best needs its day and estimate."))
        }
        self.init(
            day: day, estimatedOneRepMax: estimate,
            load: container.lenient("load", or: 0), reps: container.lenient("reps", or: 0))
    }
}

extension CardioMeasure: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "measured":
            self = .measured(
                zone2Minutes: container.lenient("zone2Minutes", or: 0),
                qualityMinutes: container.lenient("qualityMinutes", or: 0),
                qualitySessions: container.lenient("qualitySessions", or: 0),
                sessions: container.lenient("sessions", or: 0),
                heartRateDays: container.lenient("heartRateDays", or: 0))
        case "unknown":
            self = .unknown(CardioUnknownReason(wire: container.lenient("reason", or: "")))
        default:
            self = .unrecognised(type)
        }
    }
}
