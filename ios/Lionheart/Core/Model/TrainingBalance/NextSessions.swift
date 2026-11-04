import Foundation

/// What the athlete's next lift and next cardio should be, and which comes first — the server's
/// `NextSessions` (`GET /api/training/next/{day}`).
///
/// The recommender is the server's; this client only shows it. The reasons are lines the server
/// wrote, with no weights in them, so a weight is always a number in its own unit and formatted
/// here.
struct NextSessions: Equatable, Sendable {
    let day: CalendarDay
    let first: First
    let lift: NextLift
    let cardio: NextCardio
    let recovery: Recovery

    struct First: Equatable, Sendable {
        let session: NextSessionKind
        let reasons: [String]
    }

    /// The recovery the recommendation was read against.
    struct Recovery: Equatable, Sendable {
        let readiness: ReadinessNote
        let sleepMinutes: Double?
        let hrv: Double?
        let usualHrv: Double?
    }
}

enum NextSessionKind: Equatable, Sendable {
    case lift, cardio
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "lift": self = .lift
        case "cardio": self = .cardio
        default: self = .unrecognised(wire)
        }
    }
}

enum NextLift: Equatable, Sendable {
    case train(families: [PatternFamily], lastTrained: CalendarDay?, priorities: [NextPriority], reasons: [String])
    case nothingLogged(reasons: [String])
    case unrecognised(String)
}

/// A due family's priority lift and what to aim at on it.
struct NextPriority: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let movementDataId: UUID
    let name: String
    let aim: LiftAim

    var id: UUID { movementDataId }
}

enum LiftAim: Equatable, Sendable {
    /// A set to beat, in the lift's own unit: `load × reps`, and the estimated single it is worth.
    case beat(load: Double, reps: Int, unit: WeightUnit, estimatedOneRepMax: Double, bestToBeat: Double, onSystemLoad: Bool)
    /// Nothing recent to beat: today sets the mark.
    case establish
    case unrecognised(String)
}

enum NextCardio: Equatable, Sendable {
    case zone2(minutes: Double, reasons: [String])
    case quality(bouts: Int, boutMinutes: Double, recoveryMinutes: Double, workMinutes: Double, reasons: [String])
    case unrecognised(String)
}

// MARK: - Decoding

extension NextSessions: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "What is next needs its day."))
        }
        self.init(
            day: day,
            first: container.lenient(First.self, "first") ?? First(session: .unrecognised(""), reasons: []),
            lift: container.lenient(NextLift.self, "lift") ?? .unrecognised(""),
            cardio: container.lenient(NextCardio.self, "cardio") ?? .unrecognised(""),
            recovery: container.lenient(Recovery.self, "recovery")
                ?? Recovery(readiness: .notRead, sleepMinutes: nil, hrv: nil, usualHrv: nil))
    }
}

extension NextSessions.First: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            session: NextSessionKind(wire: container.lenient("session", or: "")),
            reasons: container.lenientList(String.self, "reasons"))
    }
}

extension NextSessions.Recovery: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            readiness: container.lenient(ReadinessNote.self, "readiness") ?? .notRead,
            sleepMinutes: container.lenient(Double.self, "sleepMinutes"),
            hrv: container.lenient(Double.self, "hrv"),
            usualHrv: container.lenient(Double.self, "usualHrv"))
    }
}

extension NextLift: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "train":
            self = .train(
                families: container.lenientList(String.self, "families").map(PatternFamily.init(wire:)),
                lastTrained: container.day("lastTrained"),
                priorities: container.lenientList(NextPriority.self, "priorities"),
                reasons: container.lenientList(String.self, "reasons"))
        case "nothingLogged":
            self = .nothingLogged(reasons: container.lenientList(String.self, "reasons"))
        default:
            self = .unrecognised(type)
        }
    }
}

extension NextPriority: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A priority needs its movement."))
        }
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            movementDataId: movementDataId,
            name: container.lenient("name", or: ""),
            aim: container.lenient(LiftAim.self, "aim") ?? .unrecognised(""))
    }
}

extension LiftAim: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "beat":
            self = .beat(
                load: container.lenient("load", or: 0),
                reps: container.lenient("reps", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                estimatedOneRepMax: container.lenient("estimatedOneRepMax", or: 0),
                bestToBeat: container.lenient("bestToBeat", or: 0),
                onSystemLoad: container.lenient("onSystemLoad", or: false))
        case "establish":
            self = .establish
        default:
            self = .unrecognised(type)
        }
    }
}

extension NextCardio: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        let reasons = container.lenientList(String.self, "reasons")
        switch type {
        case "zone2":
            self = .zone2(minutes: container.lenient("minutes", or: 0), reasons: reasons)
        case "quality":
            self = .quality(
                bouts: container.lenient("bouts", or: 0),
                boutMinutes: container.lenient("boutMinutes", or: 0),
                recoveryMinutes: container.lenient("recoveryMinutes", or: 0),
                workMinutes: container.lenient("workMinutes", or: 0),
                reasons: reasons)
        default:
            self = .unrecognised(type)
        }
    }
}
