import Foundation

/// How the athlete's days are shaped over a span — how long he sits at a time, when he moves and
/// trains, how regular his sleep is — split into weekdays and weekends and set against the equal
/// span before it.
///
/// Built on the server (`GET /api/habits`) from Oura's five-minute activity classification, the
/// sleep periods and the training log.
struct HabitsReport: Equatable, Sendable {
    let range: DayRange
    /// Oldest first; every day with an activity document, complete or not.
    let days: [HabitsDay]
    let current: HabitsSummary
    let previous: HabitsSummary
}

/// One day's habit figures.
struct HabitsDay: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let isWeekend: Bool
    /// Whether the day counts toward a summary: classified, and no more than three hours of it off
    /// the finger or lying still outside a scored sleep.
    let isComplete: Bool
    let steps: Int
    let activeMinutes: Int
    let sedentaryMinutes: Int
    /// `nil` on a day stored without a classification, which has no shape to read.
    let longestSittingMinutes: Int?
    /// Sittings of an hour or more.
    let longSittings: Int
    let bedtime: LocalClock?
    let wakeTime: LocalClock?

    var id: CalendarDay { day }
}

/// An instant with the time of day it was on the athlete's own clock.
///
/// A `Date` alone loses the offset the server sent, and the phone's zone is not always the one the
/// athlete was living in when he went to bed. The clock reading is taken from the wire's own offset.
struct LocalClock: Equatable, Sendable {
    let instant: Date
    let minutesAfterMidnight: Double

    /// Minutes after noon of the evening before — the measure a bedtime is averaged in, so 23:30
    /// and 00:30 are an hour apart.
    var minutesAfterNoon: Double { (minutesAfterMidnight - 720 + 1440).truncatingRemainder(dividingBy: 1440) }

    init(instant: Date, minutesAfterMidnight: Double) {
        self.instant = instant
        self.minutesAfterMidnight = minutesAfterMidnight
    }

    /// From an ISO-8601 instant with its offset: `2026-08-01T01:07:01-04:00` is 01:07 on his clock.
    init?(wire: String) {
        guard let instant = WireDate.instant(from: wire) else { return nil }
        let characters = Array(wire)
        guard characters.count >= 16,
              let hour = Int(String(characters[11...12])), let minute = Int(String(characters[14...15]))
        else { return nil }
        self.init(instant: instant, minutesAfterMidnight: Double(hour * 60 + minute))
    }
}

/// A span's habits for weekdays, for weekends, and for every day.
struct HabitsSummary: Equatable, Sendable {
    let range: DayRange
    let weekdays: HabitsFigures
    let weekends: HabitsFigures
    let allDays: HabitsFigures
}

/// The mean complete day across a set of days, and the sleep timing of the nights ending on them.
/// Every mean is absent where the set has no complete day.
struct HabitsFigures: Equatable, Sendable {
    let completeDays: Int
    let steps: Double?
    let activeMinutes: Double?
    let sedentaryMinutes: Double?
    let longestSittingMinutes: Double?
    let longSittings: Double?
    /// Twenty-four hours, 0 to 23, on his own clock.
    let hours: [HourOfDay]
    let sleep: SleepTiming?

    static let empty = HabitsFigures(
        completeDays: 0, steps: nil, activeMinutes: nil, sedentaryMinutes: nil,
        longestSittingMinutes: nil, longSittings: nil, hours: [], sleep: nil)
}

/// One clock hour of the mean complete day, in minutes, and the sessions begun in it.
struct HourOfDay: Equatable, Sendable, Identifiable {
    let hour: Int
    /// Asleep, or lying still outside a scored sleep for half an hour or more.
    let resting: Double
    let sitting: Double
    let low: Double
    let medium: Double
    let high: Double
    /// Sessions begun in this hour across the span — a count, not a mean.
    let sessionsStarted: Int

    var id: Int { hour }
}

/// When the nights began and ended, and how much each varied night to night.
struct SleepTiming: Equatable, Sendable {
    let nights: Int
    let bedtimeMinutesAfterNoon: Double
    let bedtimeSpreadMinutes: Double
    let wakeMinutesAfterMidnight: Double
    let wakeSpreadMinutes: Double
    let midpointMinutesAfterMidnight: Double
}

/// One day as a timeline: its classified runs, its sittings, its sleep and its training.
struct HabitsDayDetail: Equatable, Sendable {
    let figures: HabitsDay
    let startsAt: Date
    let endsAt: Date
    let runs: [ActivityRun]
    let sittings: [Sitting]
    let sleep: [SleepSpan]
    let training: [TrainingSpan]
}

/// What Oura classified a stretch of the day as.
enum ActivityClass: Equatable, Sendable, CaseIterable {
    case nonWear, rest, inactive, low, medium, high, unrecognised

    init(wire: String) {
        switch wire {
        case "nonWear": self = .nonWear
        case "rest": self = .rest
        case "inactive": self = .inactive
        case "low": self = .low
        case "medium": self = .medium
        case "high": self = .high
        default: self = .unrecognised
        }
    }
}

/// Consecutive five-minute slots of one class. `isAsleep` is inside a sleep Oura scored.
struct ActivityRun: Equatable, Sendable {
    let activityClass: ActivityClass
    let start: Date
    let minutes: Int
    let isAsleep: Bool

    var end: Date { start.addingTimeInterval(Double(minutes) * 60) }
}

/// An unbroken waking sitting of half an hour or more.
struct Sitting: Equatable, Sendable {
    let start: Date
    let minutes: Int

    var end: Date { start.addingTimeInterval(Double(minutes) * 60) }

    /// From this length a sitting is a long one — the server's threshold.
    static let longMinutes = 60
}

struct SleepSpan: Equatable, Sendable {
    let start: Date
    let end: Date
    let isMainSleep: Bool
}

/// A training session inside the day.
struct TrainingSpan: Equatable, Sendable {
    /// Oura's camel-cased activity name, or `strengthTraining`, `trailRun`, `distanceAndTime` for a
    /// logged session.
    let activity: String
    let start: Date
    let end: Date
    let isLogged: Bool
}

// MARK: - Decoding

extension HabitsReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let range = container.lenient(DayRange.self, "range") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A habits report without its range."))
        }
        self.init(
            range: range,
            days: container.lenientList(HabitsDay.self, "days"),
            current: container.lenient(HabitsSummary.self, "current") ?? .empty(range),
            previous: container.lenient(HabitsSummary.self, "previous") ?? .empty(range))
    }
}

extension HabitsDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A habits day needs its day."))
        }
        self.init(
            day: day,
            isWeekend: container.lenient("isWeekend", or: false),
            isComplete: container.lenient("isComplete", or: false),
            steps: container.lenient("steps", or: 0),
            activeMinutes: container.lenient("activeMinutes", or: 0),
            sedentaryMinutes: container.lenient("sedentaryMinutes", or: 0),
            longestSittingMinutes: container.lenient(Int.self, "longestSedentaryMinutes"),
            longSittings: container.lenient("longSedentaryBouts", or: 0),
            bedtime: container.lenient(String.self, "bedtime").flatMap(LocalClock.init(wire:)),
            wakeTime: container.lenient(String.self, "wakeTime").flatMap(LocalClock.init(wire:)))
    }
}

extension HabitsSummary: Decodable {
    static func empty(_ range: DayRange) -> HabitsSummary {
        HabitsSummary(range: range, weekdays: .empty, weekends: .empty, allDays: .empty)
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let range = container.lenient(DayRange.self, "range") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A summary without its range."))
        }
        self.init(
            range: range,
            weekdays: container.lenient(HabitsFigures.self, "weekdays") ?? .empty,
            weekends: container.lenient(HabitsFigures.self, "weekends") ?? .empty,
            allDays: container.lenient(HabitsFigures.self, "allDays") ?? .empty)
    }
}

extension HabitsFigures: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            completeDays: container.lenient("completeDays", or: 0),
            steps: container.lenient(Double.self, "steps"),
            activeMinutes: container.lenient(Double.self, "activeMinutes"),
            sedentaryMinutes: container.lenient(Double.self, "sedentaryMinutes"),
            longestSittingMinutes: container.lenient(Double.self, "longestSedentaryMinutes"),
            longSittings: container.lenient(Double.self, "longSedentaryBouts"),
            hours: container.lenientList(HourOfDay.self, "hours"),
            sleep: container.lenient(SleepTiming.self, "sleep"))
    }
}

extension HourOfDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let hour = container.lenient(Int.self, "hour") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "An hour needs its hour."))
        }
        self.init(
            hour: hour,
            resting: container.lenient("resting", or: 0),
            sitting: container.lenient("sedentary", or: 0),
            low: container.lenient("low", or: 0),
            medium: container.lenient("medium", or: 0),
            high: container.lenient("high", or: 0),
            sessionsStarted: container.lenient("sessionsStarted", or: 0))
    }
}

extension SleepTiming: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let bedtime = container.lenient(Double.self, "bedtimeMinutesAfterNoon"),
              let wake = container.lenient(Double.self, "wakeMinutesAfterMidnight")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A sleep timing needs its bedtime and wake."))
        }
        self.init(
            nights: container.lenient("nights", or: 0),
            bedtimeMinutesAfterNoon: bedtime,
            bedtimeSpreadMinutes: container.lenient("bedtimeSpreadMinutes", or: 0),
            wakeMinutesAfterMidnight: wake,
            wakeSpreadMinutes: container.lenient("wakeSpreadMinutes", or: 0),
            midpointMinutesAfterMidnight: container.lenient("midpointMinutesAfterMidnight", or: 0))
    }
}

extension HabitsDayDetail: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let figures = container.lenient(HabitsDay.self, "figures"),
              let startsAt = container.instant("startsAt"),
              let endsAt = container.instant("endsAt")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A day timeline needs its day and bounds."))
        }
        self.init(
            figures: figures,
            startsAt: startsAt,
            endsAt: endsAt,
            runs: container.lenientList(ActivityRun.self, "runs"),
            sittings: container.lenientList(Sitting.self, "sedentaryBouts"),
            sleep: container.lenientList(SleepSpan.self, "sleep"),
            training: container.lenientList(TrainingSpan.self, "training"))
    }
}

extension ActivityRun: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let start = container.instant("start"), let minutes = container.lenient(Int.self, "minutes") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A run needs its start and length."))
        }
        self.init(
            activityClass: ActivityClass(wire: container.lenient("class", or: "")),
            start: start,
            minutes: minutes,
            isAsleep: container.lenient("isAsleep", or: false))
    }
}

extension Sitting: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let start = container.instant("start"), let minutes = container.lenient(Int.self, "minutes") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A sitting needs its start and length."))
        }
        self.init(start: start, minutes: minutes)
    }
}

extension SleepSpan: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let start = container.instant("start"), let end = container.instant("end") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A sleep needs its span."))
        }
        self.init(start: start, end: end, isMainSleep: container.lenient("isMainSleep", or: false))
    }
}

extension TrainingSpan: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let start = container.instant("start"), let end = container.instant("end") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A session needs its span."))
        }
        self.init(
            activity: container.lenient("activity", or: ""),
            start: start,
            end: end,
            isLogged: container.lenient("isLogged", or: false))
    }
}
