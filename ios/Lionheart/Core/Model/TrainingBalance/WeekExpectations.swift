import Foundation

/// What the seven days ending on a day ask of the athlete — the server's `WeekExpectations`, carried
/// on the training score.
///
/// Every target the score, the adjusted score, weekly volume and what's next read, measurement by
/// measurement: what was done, the raw target the score reads, the target fitted to the days not
/// marked non-training, and the server's reasons for both. The client states them; it never works a
/// target out again.
struct WeekExpectations: Equatable, Sendable {
    let window: DayRange
    let availability: AvailabilityCalendar
    let sessions: Expectation
    let families: [FamilyExpectation]
    let cardio: CardioExpectations
    let priorities: [PriorityAim]
}

/// One measurement's expectation for the week.
struct Expectation: Equatable, Sendable {
    let done: Double
    /// The target the training score reads, whatever days were available.
    let raw: WeeklyTarget
    /// `raw` prorated to the window's available days; the same as it when every day was.
    let adjusted: WeeklyTarget
    /// Why the raw target is what it is, then what the days changed.
    let reasons: [String]

    /// Whether the days marked off lowered the target.
    var isLowered: Bool { adjusted.target < raw.target }

    /// Whether the week asks for anything: not when every day of it was marked off.
    var asksAnything: Bool { adjusted.committed > 0 }
}

struct FamilyExpectation: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let sets: Expectation
    let lastTrained: CalendarDay?

    var id: String { family.wire }
}

/// Zone-2 and quality minutes; `zones` is empty when the week's cardio could not be read.
struct CardioExpectations: Equatable, Sendable {
    let measure: CardioMeasure
    let zones: [ZoneExpectation]
}

struct ZoneExpectation: Equatable, Sendable {
    let band: ZoneBand
    let minutes: Expectation
}

/// A family's priority lift, its next top set, and how often the week asks for it.
struct PriorityAim: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let movementDataId: UUID
    let name: String
    let aim: LiftAim
    let frequency: PriorityFrequency
    /// Why the aim steps, holds or establishes, and how far a pinned target is.
    let reasons: [String]

    var id: UUID { movementDataId }
}

enum LiftAim: Equatable, Sendable {
    /// The last session's best set one step on; `load` is the system load where `onSystemLoad`.
    case beat(load: Double, reps: Int, unit: WeightUnit, estimatedOneRepMax: Double, bestToBeat: Double, onSystemLoad: Bool)
    /// The last session's best set again: the going-in rating, a hard last session or the week's
    /// fatigue asked for the weight to be held.
    case hold(load: Double, reps: Int, unit: WeightUnit, estimatedOneRepMax: Double, onSystemLoad: Bool)
    /// Nothing in the last twelve weeks to build on.
    case establish
    case unrecognised(String)
}

/// How often the week asks for a priority lift.
enum PriorityFrequency: Equatable, Sendable {
    /// The pin's sessions a week, prorated to the days available, against the sessions that trained it.
    case pinned(Expectation)
    /// No pin asks a frequency; the sessions of the week that trained it.
    case notPinned(sessions: Int)
    case unrecognised(String)
}

// MARK: - Decoding

extension WeekExpectations: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window"),
              let sessions = container.lenient(Expectation.self, "sessions")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "Expectations need their window and sessions."))
        }
        self.init(
            window: window,
            availability: container.lenient(AvailabilityCalendar.self, "availability")
                ?? AvailabilityCalendar(range: window, days: [], availableDays: 7),
            sessions: sessions,
            families: container.lenientList(FamilyExpectation.self, "families"),
            cardio: container.lenient(CardioExpectations.self, "cardio")
                ?? CardioExpectations(measure: .unrecognised(""), zones: []),
            priorities: container.lenientList(PriorityAim.self, "priorities"))
    }
}

extension Expectation: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let raw = container.lenient(WeeklyTarget.self, "raw") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "An expectation needs its raw target."))
        }
        self.init(
            done: container.lenient("done", or: 0),
            raw: raw,
            adjusted: container.lenient(WeeklyTarget.self, "adjusted") ?? raw,
            reasons: container.lenientList(String.self, "reasons"))
    }
}

extension FamilyExpectation: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let sets = container.lenient(Expectation.self, "sets") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A family's expectation needs its sets."))
        }
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            sets: sets,
            lastTrained: container.day("lastTrained"))
    }
}

extension CardioExpectations: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            measure: container.lenient(CardioMeasure.self, "measure") ?? .unrecognised(""),
            zones: container.lenientList(ZoneExpectation.self, "zones"))
    }
}

extension ZoneExpectation: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let minutes = container.lenient(Expectation.self, "minutes") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A band's expectation needs its minutes."))
        }
        self.init(band: ZoneBand(wire: container.lenient("band", or: "")), minutes: minutes)
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
            aim: container.lenient(LiftAim.self, "aim") ?? .unrecognised(""),
            frequency: container.lenient(PriorityFrequency.self, "frequency") ?? .unrecognised(""),
            reasons: container.lenientList(String.self, "reasons"))
    }
}

extension LiftAim: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        let load: Double = container.lenient("load", or: 0)
        let reps: Int = container.lenient("reps", or: 0)
        let unit = WeightUnit(wire: container.lenient(Int.self, "unit"))
        let estimate: Double = container.lenient("estimatedOneRepMax", or: 0)
        let onSystemLoad: Bool = container.lenient("onSystemLoad", or: false)
        switch type {
        case "beat":
            self = .beat(
                load: load, reps: reps, unit: unit, estimatedOneRepMax: estimate,
                bestToBeat: container.lenient("bestToBeat", or: 0), onSystemLoad: onSystemLoad)
        case "hold":
            self = .hold(load: load, reps: reps, unit: unit, estimatedOneRepMax: estimate, onSystemLoad: onSystemLoad)
        case "establish":
            self = .establish
        default:
            self = .unrecognised(type)
        }
    }
}

extension PriorityFrequency: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "pinned":
            self = container.lenient(Expectation.self, "sessions").map(PriorityFrequency.pinned) ?? .unrecognised(type)
        case "notPinned":
            self = .notPinned(sessions: container.lenient("sessions", or: 0))
        default:
            self = .unrecognised(type)
        }
    }
}
