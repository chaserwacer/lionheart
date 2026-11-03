import Foundation

/// One day of wearable data.
///
/// Every panel is optional on the wire and arrives independently, so each is its own value and a
/// day with sleep but no readiness is representable. Readiness is Oura's own composite score for
/// how recovered the body is — it is not Lionheart's judgement, and nothing here recomputes it.
///
/// The day is the identity. The server's `ObjectID` is not carried, because no route takes one and
/// there is one row per athlete per day — so the date is both stable and meaningful, where a
/// server id this client never sends back would be neither.
struct OuraDay: Equatable, Sendable, Identifiable {
    let date: CalendarDay
    let readiness: Readiness?
    let sleep: Sleep?
    let activity: Activity?
    let resilience: Resilience?

    var id: CalendarDay { date }

    struct Readiness: Equatable, Sendable {
        let score: Int
        let temperatureDeviation: Double
        let hrvBalance: Int
        let restingHeartRate: Int
        let recoveryIndex: Int
        let sleepBalance: Int
    }

    struct Sleep: Equatable, Sendable {
        let score: Int
        /// Seconds of sleep, as Oura reports it.
        let totalSleep: Int
        let efficiency: Int
        let remSleep: Int
        let deepSleep: Int
    }

    struct Activity: Equatable, Sendable {
        let score: Int
        let steps: Int
        let activeCalories: Int
        let totalCalories: Int
    }

    struct Resilience: Equatable, Sendable {
        let sleepRecovery: Double
        let daytimeRecovery: Double
        let stress: Double
        let level: String
    }
}

extension OuraDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let date = container.day("date") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "An Oura day without a date cannot be placed on a timeline."))
        }

        self.init(
            date: date,
            readiness: container.lenient(Readiness.self, "readinessData"),
            sleep: container.lenient(Sleep.self, "sleepData"),
            activity: container.lenient(Activity.self, "activityData"),
            resilience: container.lenient(Resilience.self, "resilienceData"))
    }
}

extension OuraDay.Readiness: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient("readinessScore", or: 0),
            temperatureDeviation: container.lenient("temperatureDeviation", or: 0),
            hrvBalance: container.lenient("hrvBalance", or: 0),
            restingHeartRate: container.lenient("restingHeartRate", or: 0),
            recoveryIndex: container.lenient("recoveryIndex", or: 0),
            sleepBalance: container.lenient("sleepBalance", or: 0))
    }
}

extension OuraDay.Sleep: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient("sleepScore", or: 0),
            totalSleep: container.lenient("totalSleep", or: 0),
            efficiency: container.lenient("efficiency", or: 0),
            remSleep: container.lenient("remSleep", or: 0),
            deepSleep: container.lenient("deepSleep", or: 0))
    }
}

extension OuraDay.Activity: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient("activityScore", or: 0),
            steps: container.lenient("steps", or: 0),
            activeCalories: container.lenient("activeCalories", or: 0),
            totalCalories: container.lenient("totalCalories", or: 0))
    }
}

extension OuraDay.Resilience: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            sleepRecovery: container.lenient("sleepRecovery", or: 0),
            daytimeRecovery: container.lenient("daytimeRecovery", or: 0),
            stress: container.lenient("stress", or: 0),
            level: container.lenient("resilienceLevel", or: ""))
    }
}
