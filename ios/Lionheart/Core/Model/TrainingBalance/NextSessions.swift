import Foundation

/// What to train next — the server's `NextSessions` (`GET /api/training/next/{day}`): the next lift,
/// the next cardio, and which of the two first, each with the reasons it was read from.
///
/// Read by the Train tab's top section and the workout tracker's header, so both say the same thing.
struct NextSessions: Equatable, Sendable {
    let day: CalendarDay
    let first: FirstChoice
    let lift: NextLift
    let cardio: NextCardio
    let recovery: RecoveryRead
}

enum NextKind: Equatable, Sendable {
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

struct FirstChoice: Equatable, Sendable {
    let session: NextKind
    let reasons: [String]
}

enum NextLift: Equatable, Sendable {
    /// The families due, trained longest ago, and each one's priority lift with its next top set.
    case train(families: [PatternFamily], lastTrained: CalendarDay?, priorities: [PriorityAim], reasons: [String])
    case nothingLogged(reasons: [String])
    case unrecognised(String)

    var reasons: [String] {
        switch self {
        case .train(_, _, _, let reasons), .nothingLogged(let reasons): reasons
        case .unrecognised: []
        }
    }
}

/// A due family's priority lift and its next top set.
struct PriorityAim: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let movementDataId: UUID
    let name: String
    let aim: LiftAim

    var id: UUID { movementDataId }
}

enum LiftAim: Equatable, Sendable {
    /// The last session's best set one step on; `load` is the system load where `onSystemLoad`.
    case beat(load: Double, reps: Int, unit: WeightUnit, estimatedOneRepMax: Double, bestToBeat: Double, onSystemLoad: Bool)
    /// Nothing in the last twelve weeks to build on.
    case establish
    case unrecognised(String)
}

enum NextCardio: Equatable, Sendable {
    /// Continuous work at 60–70 % of maximum heart rate.
    case zone2(minutes: Int, reasons: [String])
    /// Intervals at zone 4–5 with easy recoveries between.
    case quality(bouts: Int, boutMinutes: Int, recoveryMinutes: Int, reasons: [String])
    case unrecognised(String)

    var reasons: [String] {
        switch self {
        case .zone2(_, let reasons), .quality(_, _, _, let reasons): reasons
        case .unrecognised: []
        }
    }
}

/// Last night and this morning as the recommendation read them.
struct RecoveryRead: Equatable, Sendable {
    let readiness: ReadinessNote
    let sleepMinutes: Int?
    let hrv: Int?
    let usualHrv: Double?
}

// MARK: - Decoding

extension NextSessions: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day"), let first = container.lenient(FirstChoice.self, "first") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "What is next needs its day and its first choice."))
        }
        self.init(
            day: day,
            first: first,
            lift: container.lenient(NextLift.self, "lift") ?? .unrecognised(""),
            cardio: container.lenient(NextCardio.self, "cardio") ?? .unrecognised(""),
            recovery: container.lenient(RecoveryRead.self, "recovery")
                ?? RecoveryRead(readiness: .notRead, sleepMinutes: nil, hrv: nil, usualHrv: nil))
    }
}

extension FirstChoice: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            session: NextKind(wire: container.lenient("session", or: "")),
            reasons: container.lenientList(String.self, "reasons"))
    }
}

extension NextLift: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        let reasons = container.lenientList(String.self, "reasons")
        switch type {
        case "train":
            self = .train(
                families: container.lenientList(String.self, "families").map(PatternFamily.init(wire:)),
                lastTrained: container.day("lastTrained"),
                priorities: container.lenientList(PriorityAim.self, "priorities"),
                reasons: reasons)
        case "nothingLogged":
            self = .nothingLogged(reasons: reasons)
        default:
            self = .unrecognised(type)
        }
    }
}

extension PriorityAim: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "movementDataId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A priority aim needs its movement."))
        }
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            movementDataId: id,
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
                reasons: reasons)
        default:
            self = .unrecognised(type)
        }
    }
}

extension RecoveryRead: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            readiness: container.lenient(ReadinessNote.self, "readiness") ?? .notRead,
            sleepMinutes: container.lenient(Int.self, "sleepMinutes"),
            hrv: container.lenient(Int.self, "hrv"),
            usualHrv: container.lenient(Double.self, "usualHrv"))
    }
}
