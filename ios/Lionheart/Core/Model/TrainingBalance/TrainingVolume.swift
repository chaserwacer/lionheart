import Foundation

/// The hard sets of the seven days ending on a day, by family, pattern and muscle group, each against
/// its weekly target — the server's `TrainingVolume` (`GET /api/training/volume/{day}`).
///
/// A hard set is a working, drop or failure set of a lift. Counted on the server, which holds what
/// every movement trains, so the Train page, the score and the chat model read one number.
struct TrainingVolume: Equatable, Sendable {
    let window: DayRange
    let families: [FamilyVolume]
    let patterns: [PatternVolume]
    let muscles: [MuscleVolume]
    /// Movements with hard sets in the window that nothing classifies, so they counted nowhere.
    let unplaced: [UnplacedMovement]
    let upNext: UpNext

    func family(_ family: PatternFamily) -> FamilyVolume? { families.first { $0.family == family } }
}

struct FamilyVolume: Equatable, Sendable, Identifiable {
    let family: PatternFamily
    let hardSets: Int
    let target: Int
    /// The last day a hard set of the family was logged, over the whole log; `nil` when never.
    let lastTrained: CalendarDay?

    var id: String { family.wire }
}

struct PatternVolume: Equatable, Sendable, Identifiable {
    let pattern: MovementPattern
    let hardSets: Int
    /// Absent for isolation and other work, which is counted toward muscles rather than judged.
    let target: Int?
    let lastTrained: CalendarDay?

    var id: String { pattern.wire }
}

/// One muscle's sets: whole for each set it was a primary mover in, half as a secondary one.
struct MuscleVolume: Equatable, Sendable, Identifiable {
    let muscle: MuscleGroup
    let direct: Int
    let indirect: Int
    let target: Int
    /// The last day it was trained as a primary mover.
    let lastTrained: CalendarDay?

    var sets: Double { Double(direct) + Double(indirect) / 2 }
    var id: String { muscle.wire }
}

struct UnplacedMovement: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let name: String
    let hardSets: Int

    var id: UUID { movementDataId }
}

/// What the log says comes next: the families trained longest ago, with the day's readiness beside
/// them where it is unusual. A recommendation, never an instruction.
enum UpNext: Equatable, Sendable {
    /// `families` are one to three, together when last trained the same day. `trainedToday` means
    /// this is the next session rather than today's.
    case train(families: [PatternFamily], lastTrained: CalendarDay?, trainedToday: Bool, readiness: ReadinessNote)
    /// No classified lifting in the history at all.
    case nothingLogged
    case unrecognised(String)
}

/// Oura's readiness score for the day against the athlete's last ninety days. `ouraScore` is
/// Oura's 0–100 rating of the day, not a measurement.
enum ReadinessNote: Equatable, Sendable {
    case notRead
    case usual(ouraScore: Int)
    /// Under the tenth percentile of the last ninety days, which spanned `usualLow`–`usualHigh`.
    case low(ouraScore: Int, usualLow: Int, usualHigh: Int)
    case unrecognised(String)
}

// MARK: - Decoding

extension TrainingVolume: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let window = container.lenient(DayRange.self, "window") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "Weekly volume needs its window."))
        }
        self.init(
            window: window,
            families: container.lenientList(FamilyVolume.self, "families"),
            patterns: container.lenientList(PatternVolume.self, "patterns"),
            muscles: container.lenientList(MuscleVolume.self, "muscles"),
            unplaced: container.lenientList(UnplacedMovement.self, "unplaced"),
            upNext: container.lenient(UpNext.self, "upNext") ?? .unrecognised(""))
    }
}

extension FamilyVolume: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            hardSets: container.lenient("hardSets", or: 0),
            target: container.lenient("target", or: 0),
            lastTrained: container.day("lastTrained"))
    }
}

extension PatternVolume: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            pattern: MovementPattern(wire: container.lenient("pattern", or: "")),
            hardSets: container.lenient("hardSets", or: 0),
            target: container.lenient(Int.self, "target"),
            lastTrained: container.day("lastTrained"))
    }
}

extension MuscleVolume: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            muscle: MuscleGroup(wire: container.lenient("muscle", or: "")),
            direct: container.lenient("direct", or: 0),
            indirect: container.lenient("indirect", or: 0),
            target: container.lenient("target", or: 0),
            lastTrained: container.day("lastTrained"))
    }
}

extension UnplacedMovement: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "An unplaced movement needs its id."))
        }
        self.init(
            movementDataId: movementDataId,
            name: container.lenient("name", or: ""),
            hardSets: container.lenient("hardSets", or: 0))
    }
}

extension UpNext: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "train":
            self = .train(
                families: container.lenientList(String.self, "families").map(PatternFamily.init(wire:)),
                lastTrained: container.day("lastTrained"),
                trainedToday: container.lenient("trainedToday", or: false),
                readiness: container.lenient(ReadinessNote.self, "readiness") ?? .notRead)
        case "nothingLogged":
            self = .nothingLogged
        default:
            self = .unrecognised(type)
        }
    }
}

extension ReadinessNote: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "notRead":
            self = .notRead
        case "usual":
            self = .usual(ouraScore: container.lenient("ouraScore", or: 0))
        case "low":
            self = .low(
                ouraScore: container.lenient("ouraScore", or: 0),
                usualLow: container.lenient("usualLow", or: 0),
                usualHigh: container.lenient("usualHigh", or: 0))
        default:
            self = .unrecognised(type)
        }
    }
}
