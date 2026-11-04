import Foundation

/// How ready the athlete is for the session he is about to train — the server's `GoingInState`
/// (`GET /api/training/going-in`): one rating from five parts, each with the figures it was read
/// from, and what to note before the first set.
///
/// Read on the server because it is built from the chat briefing's own facts, so the rating and what
/// the chat says about today are one reading. Every part is 0–100 with his usual day near 75.
struct GoingInState: Equatable, Sendable {
    let asOf: Date
    let rating: GoingInRating
    let readiness: ReadinessPart
    let autonomic: AutonomicPart
    let sleep: SleepPart
    let load: LoadPart
    let wellness: WellnessPart
    let injuries: [InjuryNote]
    /// Planned sessions dated today, each as the lifts it prescribes, by name.
    let plannedToday: [[String]]
    let recentMuscles: [RecentMuscle]

    struct ReadinessPart: Equatable, Sendable {
        let score: PartScore
        let ouraScore: OuraScore?
        let usualLow: OuraScore?
        let usualHigh: OuraScore?
        let hrvBalance: OuraScore?
        /// Oura's rating of the resting heart rate against his baseline, not a heart rate.
        let restingHeartRate: OuraScore?
        /// Degrees Celsius from his baseline, Oura's own deviation.
        let temperatureDeviation: Double?
    }

    /// Last night's HRV and lowest heart rate against the nights before the last two.
    struct AutonomicPart: Equatable, Sendable {
        let score: PartScore
        /// Milliseconds.
        let hrv: Int?
        let hrvBaseline: Double?
        /// Beats per minute.
        let lowestHeartRate: Int?
        let lowestHeartRateBaseline: Double?
        let baselineNights: Int
    }

    struct SleepPart: Equatable, Sendable {
        let score: PartScore
        let lastNight: NightNote?
        let nightBefore: NightNote?
    }

    /// One night as Oura filed it, under the day he woke.
    struct NightNote: Equatable, Sendable {
        let day: CalendarDay
        let totalSleep: TimeInterval
        /// Per cent of the time in bed spent asleep.
        let efficiency: Int
        let sleepScore: OuraScore?
    }

    /// The training since the start of yesterday, and the week against the month.
    struct LoadPart: Equatable, Sendable {
        let score: PartScore
        let hardSets: Int
        /// Absent when the cardio could not be read, which is not a day without cardio.
        let highIntensityMinutes: Double?
        let zoneTwoMinutes: Double?
        let hardSetsLast7: Int
        let hardSetsWeeklyMean28: Double
    }

    struct WellnessPart: Equatable, Sendable {
        let score: PartScore
        let today: WellnessNote?
    }

    /// The four 1–5 ratings and their mean, stress inverted in the mean.
    struct WellnessNote: Equatable, Sendable {
        let overall: Double
        let mood: Int
        let energy: Int
        let stress: Int
        let motivation: Int
    }

    struct InjuryNote: Equatable, Sendable, Identifiable {
        let id: UUID
        let name: String
        /// The newest check-in's pain, 0–10.
        let lastPain: Int?
        /// The movements its check-ins name.
        let movementDataIds: [UUID]
    }

    struct RecentMuscle: Equatable, Sendable {
        let muscle: MuscleGroup
        let lastTrained: CalendarDay
    }
}

/// The going-in rating, or that there was nothing about the body today to rate it from.
enum GoingInRating: Equatable, Sendable {
    case rated(score: Int, band: GoingInBand)
    /// No readiness, no night and no sleep score: load and wellness alone say nothing about recovery.
    case notRated
    case unrecognised(String)

    var score: Int? {
        switch self {
        case .rated(let score, _): score
        case .notRated, .unrecognised: nil
        }
    }
}

enum GoingInBand: Equatable, Sendable {
    case high, normal, reduced, low
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "high": self = .high
        case "normal": self = .normal
        case "reduced": self = .reduced
        case "low": self = .low
        default: self = .unrecognised(wire)
        }
    }
}

/// A part's score and the share of the rating it carried, or that it could not be scored.
enum PartScore: Equatable, Sendable {
    case scored(score: Int, share: Double)
    case notScored
    case unrecognised(String)

    var score: Int? {
        switch self {
        case .scored(let score, _): score
        case .notScored, .unrecognised: nil
        }
    }
}

// MARK: - Decoding

extension GoingInState: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let asOf = container.instant("asOf") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A going-in state needs its moment."))
        }
        self.init(
            asOf: asOf,
            rating: container.lenient(GoingInRating.self, "rating") ?? .unrecognised(""),
            readiness: container.lenient(ReadinessPart.self, "readiness")
                ?? ReadinessPart(score: .notScored, ouraScore: nil, usualLow: nil, usualHigh: nil,
                                 hrvBalance: nil, restingHeartRate: nil, temperatureDeviation: nil),
            autonomic: container.lenient(AutonomicPart.self, "autonomic")
                ?? AutonomicPart(score: .notScored, hrv: nil, hrvBaseline: nil, lowestHeartRate: nil,
                                 lowestHeartRateBaseline: nil, baselineNights: 0),
            sleep: container.lenient(SleepPart.self, "sleep")
                ?? SleepPart(score: .notScored, lastNight: nil, nightBefore: nil),
            load: container.lenient(LoadPart.self, "load")
                ?? LoadPart(score: .notScored, hardSets: 0, highIntensityMinutes: nil, zoneTwoMinutes: nil,
                            hardSetsLast7: 0, hardSetsWeeklyMean28: 0),
            wellness: container.lenient(WellnessPart.self, "wellness") ?? WellnessPart(score: .notScored, today: nil),
            injuries: container.lenientList(InjuryNote.self, "injuries"),
            plannedToday: container.lenientList(PlannedNote.self, "plannedToday").map(\.movements),
            recentMuscles: container.lenientList(RecentMuscle.self, "recentMuscles"))
    }

    private struct PlannedNote: Decodable {
        let movements: [String]

        init(from decoder: any Decoder) throws {
            movements = try decoder.container(keyedBy: WireKey.self).lenientList(String.self, "movements")
        }
    }
}

extension GoingInRating: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "rated":
            // A rating without its number is not a rating of nought, which would read as "0 Low".
            guard let score = container.lenient(Int.self, "score") else { self = .unrecognised(type); return }
            self = .rated(score: score, band: GoingInBand(wire: container.lenient("band", or: "")))
        case "notRated":
            self = .notRated
        default:
            self = .unrecognised(type)
        }
    }
}

extension PartScore: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "scored":
            guard let score = container.lenient(Int.self, "score") else { self = .unrecognised(type); return }
            self = .scored(score: score, share: container.lenient("share", or: 0))
        case "notScored":
            self = .notScored
        default:
            self = .unrecognised(type)
        }
    }
}

private extension KeyedDecodingContainer where Key == WireKey {
    func partScore() -> PartScore { lenient(PartScore.self, "score") ?? .unrecognised("") }

    func ouraScore(_ key: String) -> OuraScore? { lenient(Double.self, key).map(OuraScore.init) }
}

extension GoingInState.ReadinessPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.partScore(),
            ouraScore: container.ouraScore("ouraScore"),
            usualLow: container.ouraScore("usualLow"),
            usualHigh: container.ouraScore("usualHigh"),
            hrvBalance: container.ouraScore("hrvBalance"),
            restingHeartRate: container.ouraScore("restingHeartRate"),
            temperatureDeviation: container.lenient(Double.self, "temperatureDeviation"))
    }
}

extension GoingInState.AutonomicPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.partScore(),
            hrv: container.lenient(Int.self, "hrv"),
            hrvBaseline: container.lenient(Double.self, "hrvBaseline"),
            lowestHeartRate: container.lenient(Int.self, "lowestHeartRate"),
            lowestHeartRateBaseline: container.lenient(Double.self, "lowestHeartRateBaseline"),
            baselineNights: container.lenient("baselineNights", or: 0))
    }
}

extension GoingInState.SleepPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.partScore(),
            lastNight: container.lenient(GoingInState.NightNote.self, "lastNight"),
            nightBefore: container.lenient(GoingInState.NightNote.self, "nightBefore"))
    }
}

extension GoingInState.NightNote: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "A night needs its day."))
        }
        self.init(
            day: day,
            totalSleep: TimeInterval(container.lenient("totalSleepSeconds", or: 0)),
            efficiency: container.lenient("efficiency", or: 0),
            sleepScore: container.ouraScore("sleepScore"))
    }
}

extension GoingInState.LoadPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.partScore(),
            hardSets: container.lenient("hardSets", or: 0),
            highIntensityMinutes: container.lenient(Double.self, "highIntensityMinutes"),
            zoneTwoMinutes: container.lenient(Double.self, "zoneTwoMinutes"),
            hardSetsLast7: container.lenient("hardSetsLast7", or: 0),
            hardSetsWeeklyMean28: container.lenient("hardSetsWeeklyMean28", or: 0))
    }
}

extension GoingInState.WellnessPart: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(score: container.partScore(), today: container.lenient(GoingInState.WellnessNote.self, "today"))
    }
}

extension GoingInState.WellnessNote: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            overall: container.lenient("overall", or: 0),
            mood: container.lenient("mood", or: 0),
            energy: container.lenient("energy", or: 0),
            stress: container.lenient("stress", or: 0),
            motivation: container.lenient("motivation", or: 0))
    }
}

extension GoingInState.InjuryNote: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "injuryId") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "An injury needs its id."))
        }
        self.init(
            id: id,
            name: container.lenient("name", or: ""),
            lastPain: container.lenient(Int.self, "lastPain"),
            movementDataIds: container.lenientList(UUID.self, "movementDataIds"))
    }
}

extension GoingInState.RecentMuscle: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("lastTrained") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "A recent muscle needs its day."))
        }
        self.init(muscle: MuscleGroup(wire: container.lenient("muscle", or: "")), lastTrained: day)
    }
}
