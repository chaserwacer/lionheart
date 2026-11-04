import Foundation

/// The athlete's cardio over a span: every session with its heart-rate zones, the weeks they add up
/// to, Oura's fitness estimates, and whether the aerobic base is moving.
///
/// Built on the server (`GET /api/cardio`), because it is read from the all-day heart-rate series —
/// tens of thousands of samples a month — and the answer is a few hundred numbers. The client draws
/// it and derives nothing it could get wrong differently from the server.
struct CardioReport: Equatable, Sendable {
    let range: DayRange
    /// What the zones are drawn from, or `nil` when the profile has no usable age and the ring no
    /// peak — then no session carries zones either.
    let maxHeartRate: MaxHeartRate?
    /// Oldest first.
    let sessions: [CardioSession]
    /// Every Monday-to-Sunday week the span touches, oldest first, empty weeks included.
    let weeks: [CardioWeek]
    let vo2Max: [DayFigure]
    let cardiovascularAge: [DayFigure]
    /// The night's lowest heart rate, per waking day.
    let restingHeartRate: [DayFigure]
    let aerobicBase: AerobicBase
    let excluded: [ExcludedWorkouts]
}

/// One figure on one day, as the server sent it.
struct DayFigure: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let value: Double

    var id: CalendarDay { day }
}

/// Where the zones' maximum came from. Two sources, neither a measured maximum, so the screen says
/// which one is in use.
enum MaxHeartRate: Equatable, Sendable {
    /// Tanaka's prediction from the profile's age, with the ring's own cardio peak where there were
    /// enough samples to read one — reported, but below the prediction, so not used.
    case agePredicted(bpm: Int, age: Int, observedPeak: Int?)
    /// The ring's own peak, which exceeded the age prediction.
    case observed(bpm: Int, agePredictedBpm: Int, age: Int)
    case unrecognised(String)

    var bpm: Int? {
        switch self {
        case .agePredicted(let bpm, _, _), .observed(let bpm, _, _): bpm
        case .unrecognised: nil
        }
    }
}

/// One cardio session: a run the athlete logged, or a workout the ring saw.
struct CardioSession: Equatable, Sendable, Identifiable {
    let source: Source
    let start: Date
    let end: Date
    let distanceMeters: Double?
    let heartRate: SessionHeartRate

    var minutes: Double { end.timeIntervalSince(start) / 60 }

    var id: String {
        switch source {
        case .ringWorkout(let id, _, _): id.uuidString
        case .loggedSession(let id, _, _, _): id.uuidString
        case .unrecognised(let type): "\(type)-\(start.timeIntervalSince1970)"
        }
    }

    enum Source: Equatable, Sendable {
        case ringWorkout(ouraId: UUID, activity: String, category: WorkoutCategory)
        /// `isRecorded`: imported from Apple Health, so its page has the route and the recording
        /// app's own figures. `activity`: what an endurance session was — a ride, a hike, a walk —
        /// and `nil` for a trail run or a distance-and-time session.
        case loggedSession(trainingSessionId: UUID, isTrailRun: Bool, isRecorded: Bool, activity: EnduranceActivity?)
        case unrecognised(String)
    }
}

/// What kind of effort a ring workout was, as the server classified it.
enum WorkoutCategory: Equatable, Sendable {
    case endurance, sport, walk, strength, incidental
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "endurance": self = .endurance
        case "sport": self = .sport
        case "walk": self = .walk
        case "strength": self = .strength
        case "incidental": self = .incidental
        default: self = .unrecognised(wire)
        }
    }
}

/// What the heart rate held across a session: the recording app's, or the ring's all-day series.
enum SessionHeartRate: Equatable, Sendable {
    /// No sample inside the session: the ring was off, or the series is not synced that far back.
    case notRecorded
    /// `coverage` is the share of the session's clock time the samples stand for, 0 to 1. `zones`
    /// is absent only when there is no maximum to draw them from.
    case recorded(averageBpm: Int, peakBpm: Int, coverage: Double, zones: ZoneMinutes?, source: HeartRateSource)
    case unrecognised(String)
}

/// Whose samples a session's heart rate was read from.
enum HeartRateSource: Equatable, Sendable {
    /// The ring's all-day series over the session's clock time.
    case ring
    /// The samples the app that recorded the workout wrote to Apple Health.
    case appleHealth
    case unrecognised(String)

    /// A reading with no source is the ring's: until runs came from Health, every one was.
    init(wire: String?) {
        switch wire {
        case nil, "ring": self = .ring
        case "appleHealth": self = .appleHealth
        case .some(let other): self = .unrecognised(other)
        }
    }
}

/// Minutes in each of the five heart-rate zones, and below the first.
struct ZoneMinutes: Equatable, Sendable {
    let belowZones: Double
    let zone1: Double
    let zone2: Double
    let zone3: Double
    let zone4: Double
    let zone5: Double
    let inZones: Double
    /// Edwards' load, as the server computed it.
    let load: Double

    static let none = ZoneMinutes(
        belowZones: 0, zone1: 0, zone2: 0, zone3: 0, zone4: 0, zone5: 0, inZones: 0, load: 0)

    /// Zone 1 to 5, in order.
    var byZone: [Double] { [zone1, zone2, zone3, zone4, zone5] }

    /// The lower edge of each zone as a fraction of maximum — the server's scheme, restated here
    /// only so a screen can label the bands in beats per minute.
    static let lowerEdges: [Double] = [0.5, 0.6, 0.7, 0.8, 0.9]
}

struct CardioWeek: Equatable, Sendable, Identifiable {
    let weekStarting: CalendarDay
    /// How many of its seven days the span covers — fewer at the span's ends.
    let daysInRange: Int
    let sessions: Int
    let minutes: Double
    let zones: ZoneMinutes

    var id: CalendarDay { weekStarting }
}

struct ExcludedWorkouts: Equatable, Sendable, Identifiable {
    let category: WorkoutCategory
    let reason: Reason
    let count: Int

    var id: String { "\(reason)-\(category)" }

    /// Why the server left a ring workout out of the sessions.
    enum Reason: Equatable, Sendable {
        case sameAsLoggedRun, insideLoggedLifting, shortWalk, strength, notTraining, unclassified, noDuration
        case unrecognised(String)

        init(wire: String) {
            switch wire {
            case "sameAsLoggedRun": self = .sameAsLoggedRun
            case "insideLoggedLifting": self = .insideLoggedLifting
            case "shortWalk": self = .shortWalk
            case "strength": self = .strength
            case "notTraining": self = .notTraining
            case "unclassified": self = .unclassified
            case "noDuration": self = .noDuration
            default: self = .unrecognised(wire)
            }
        }

        /// The reason as a phrase after a count: "330 walks under 30 minutes".
        var phrase: String {
            switch self {
            case .sameAsLoggedRun: "the same session as a logged run"
            case .insideLoggedLifting: "inside a logged lifting session"
            case .shortWalk: "walks under 30 minutes"
            case .strength: "strength work"
            case .notTraining: "housework, yard work or stretching"
            case .unclassified: "activities not yet classified"
            case .noDuration: "with no duration"
            case .unrecognised(let wire): wire
            }
        }
    }
}

/// The last four weeks against the eight before.
struct AerobicBase: Equatable, Sendable {
    let recent: DayRange
    let before: DayRange
    let zone2MinutesPerWeek: SpanComparison
    let cardioMinutesPerWeek: SpanComparison
    let vo2Max: SpanComparison
    let restingHeartRate: SpanComparison
    let cardiovascularAge: SpanComparison
}

/// A figure over a recent span and over the span before it. Either is absent when its span has no
/// data, which is a different answer from zero.
struct SpanComparison: Equatable, Sendable {
    let recent: Double?
    let before: Double?

    var change: Double? {
        guard let recent, let before else { return nil }
        return recent - before
    }

    static let empty = SpanComparison(recent: nil, before: nil)
}

// MARK: - Decoding

extension CardioReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let range = container.lenient(DayRange.self, "range") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A cardio report without its range."))
        }
        self.init(
            range: range,
            maxHeartRate: container.lenient(MaxHeartRate.self, "maxHeartRate"),
            sessions: container.lenientList(CardioSession.self, "sessions"),
            weeks: container.lenientList(CardioWeek.self, "weeks"),
            vo2Max: container.lenientList(DayFigure.self, "vo2Max"),
            cardiovascularAge: container.lenientList(DayFigure.self, "cardiovascularAge"),
            restingHeartRate: container.lenientList(DayFigure.self, "restingHeartRate"),
            aerobicBase: container.lenient(AerobicBase.self, "aerobicBase") ?? .empty(range),
            excluded: container.lenientList(ExcludedWorkouts.self, "excluded"))
    }
}

extension DayFigure: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day"), let value = container.lenient(Double.self, "value") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A figure needs its day and value."))
        }
        self.init(day: day, value: value)
    }
}

extension MaxHeartRate: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "agePredicted":
            guard let bpm = container.lenient(Int.self, "bpm"), let age = container.lenient(Int.self, "age") else {
                self = .unrecognised(type)
                return
            }
            self = .agePredicted(bpm: bpm, age: age, observedPeak: container.lenient(Int.self, "observedPeak"))
        case "observed":
            guard let bpm = container.lenient(Int.self, "bpm"),
                  let predicted = container.lenient(Int.self, "agePredictedBpm"),
                  let age = container.lenient(Int.self, "age")
            else {
                self = .unrecognised(type)
                return
            }
            self = .observed(bpm: bpm, agePredictedBpm: predicted, age: age)
        default:
            self = .unrecognised(type)
        }
    }
}

extension CardioSession: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let start = container.instant("start"), let end = container.instant("end") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A session must sit on the clock."))
        }
        self.init(
            source: container.lenient(Source.self, "source") ?? .unrecognised(""),
            start: start,
            end: end,
            distanceMeters: container.lenient(Double.self, "distanceMeters"),
            heartRate: container.lenient(SessionHeartRate.self, "heartRate") ?? .unrecognised(""))
    }
}

extension CardioSession.Source: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "ringWorkout":
            guard let id = container.lenient(UUID.self, "ouraId") else { self = .unrecognised(type); return }
            self = .ringWorkout(
                ouraId: id,
                activity: container.lenient("activity", or: ""),
                category: WorkoutCategory(wire: container.lenient("category", or: "")))
        case "loggedSession":
            guard let id = container.lenient(UUID.self, "trainingSessionId") else { self = .unrecognised(type); return }
            self = .loggedSession(
                trainingSessionId: id,
                isTrailRun: container.lenient("isTrailRun", or: false),
                isRecorded: container.lenient("isRecorded", or: false),
                activity: container.lenient(String.self, "activity").map(EnduranceActivity.init(wire:)))
        default:
            self = .unrecognised(type)
        }
    }
}

extension SessionHeartRate: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "notRecorded":
            self = .notRecorded
        case "recorded":
            guard let average = container.lenient(Int.self, "averageBpm"),
                  let peak = container.lenient(Int.self, "peakBpm")
            else {
                self = .unrecognised(type)
                return
            }
            self = .recorded(
                averageBpm: average,
                peakBpm: peak,
                coverage: container.lenient("coverage", or: 0),
                zones: container.lenient(ZoneMinutes.self, "zones"),
                source: HeartRateSource(wire: container.lenient(String.self, "source")))
        default:
            self = .unrecognised(type)
        }
    }
}

extension ZoneMinutes: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            belowZones: container.lenient("belowZones", or: 0),
            zone1: container.lenient("zone1", or: 0),
            zone2: container.lenient("zone2", or: 0),
            zone3: container.lenient("zone3", or: 0),
            zone4: container.lenient("zone4", or: 0),
            zone5: container.lenient("zone5", or: 0),
            inZones: container.lenient("inZones", or: 0),
            load: container.lenient("load", or: 0))
    }
}

extension CardioWeek: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let week = container.day("weekStarting") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A week needs its first day."))
        }
        self.init(
            weekStarting: week,
            daysInRange: container.lenient("daysInRange", or: 7),
            sessions: container.lenient("sessions", or: 0),
            minutes: container.lenient("minutes", or: 0),
            zones: container.lenient(ZoneMinutes.self, "zones") ?? .none)
    }
}

extension ExcludedWorkouts: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            category: WorkoutCategory(wire: container.lenient("category", or: "")),
            reason: Reason(wire: container.lenient("reason", or: "")),
            count: container.lenient("count", or: 0))
    }
}

extension AerobicBase: Decodable {
    static func empty(_ range: DayRange) -> AerobicBase {
        AerobicBase(
            recent: range, before: range, zone2MinutesPerWeek: .empty, cardioMinutesPerWeek: .empty,
            vo2Max: .empty, restingHeartRate: .empty, cardiovascularAge: .empty)
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let recent = container.lenient(DayRange.self, "recent"),
              let before = container.lenient(DayRange.self, "before")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A comparison needs its two windows."))
        }
        self.init(
            recent: recent,
            before: before,
            zone2MinutesPerWeek: container.lenient(SpanComparison.self, "zone2MinutesPerWeek") ?? .empty,
            cardioMinutesPerWeek: container.lenient(SpanComparison.self, "cardioMinutesPerWeek") ?? .empty,
            vo2Max: container.lenient(SpanComparison.self, "vo2Max") ?? .empty,
            restingHeartRate: container.lenient(SpanComparison.self, "restingHeartRate") ?? .empty,
            cardiovascularAge: container.lenient(SpanComparison.self, "cardiovascularAge") ?? .empty)
    }
}

extension SpanComparison: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(recent: container.lenient(Double.self, "recent"), before: container.lenient(Double.self, "before"))
    }
}
