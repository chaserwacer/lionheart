import Foundation

/// Everything the ring recorded about one calendar day.
///
/// Six collections, each independently absent. Oura files them separately and the ring is off the
/// finger for stretches, so a day with a sleep score and no readiness is an ordinary row rather
/// than a damaged one — which is why every panel is an optional value and not a zeroed struct.
///
/// **Absence is honest here, and nothing in the client should second-guess it.** A collection Oura
/// filed nothing for arrives `null`; a day the ring recorded nothing for is simply not in the
/// range, and asking for it by date answers 404. So a score of zero is a score Oura issued, and a
/// client that dropped it would be hiding real data.
///
/// The day is the identity: one document set per athlete per day, and no route takes Oura's own
/// id for a daily collection.
struct OuraDay: Equatable, Sendable, Identifiable {
    let date: CalendarDay
    let activity: Activity?
    let readiness: Readiness?
    let sleep: Sleep?
    let spo2: Spo2?
    let stress: Stress?
    let resilience: Resilience?

    var id: CalendarDay { date }

    /// Whether the ring filed anything at all for this day.
    var isEmpty: Bool {
        activity == nil && readiness == nil && sleep == nil
            && spo2 == nil && stress == nil && resilience == nil
    }

    /// How much the athlete moved, and Oura's score for it.
    struct Activity: Equatable, Sendable {
        let score: Int?
        let steps: Int
        let activeCalories: Int
        let totalCalories: Int
        let targetCalories: Int
        let equivalentWalkingDistanceMeters: Int
        let inactivityAlerts: Int
        /// A unitless mean multiple of resting metabolic rate, unlike the MET *minute* sums.
        let averageMet: Double
        let lowActivityMetMinutes: Int
        let mediumActivityMetMinutes: Int
        let highActivityMetMinutes: Int
        let sedentaryMetMinutes: Int
        let restingTimeSeconds: Int
        let sedentaryTimeSeconds: Int
        let lowActivityTimeSeconds: Int
        let mediumActivityTimeSeconds: Int
        let highActivityTimeSeconds: Int
        let nonWearTimeSeconds: Int
        let contributors: Contributors

        /// The six components Oura's activity score is built from, each 1–100.
        struct Contributors: Equatable, Sendable {
            let meetDailyTargets: Int?
            let moveEveryHour: Int?
            let recoveryTime: Int?
            let stayActive: Int?
            let trainingFrequency: Int?
            let trainingVolume: Int?
        }
    }

    /// Oura's read on how recovered the body is, and the components behind it.
    struct Readiness: Equatable, Sendable {
        let score: Int?
        let temperatureDeviation: Double?
        let temperatureTrendDeviation: Double?
        let contributors: Contributors

        /// The nine components Oura's readiness score is built from, each 1–100.
        ///
        /// Every one is a **score comparing the day against the athlete's own baseline**, never a
        /// measurement. `restingHeartRate` is how his resting rate compared — it is not a heart
        /// rate, and labelling it as one on a screen is the mistake this doc comment exists to
        /// prevent. The rate itself lives on the night, as `OuraSleepPeriod.lowestHeartRate`.
        struct Contributors: Equatable, Sendable {
            let activityBalance: Int?
            let bodyTemperature: Int?
            let hrvBalance: Int?
            let previousDayActivity: Int?
            let previousNight: Int?
            let recoveryIndex: Int?
            let restingHeartRate: Int?
            let sleepBalance: Int?
            let sleepRegularity: Int?
        }
    }

    /// Oura's score for the night as a whole, and the components behind it.
    struct Sleep: Equatable, Sendable {
        let score: Int?
        let contributors: Contributors

        /// The seven components Oura's sleep score is built from, each 1–100.
        struct Contributors: Equatable, Sendable {
            let deepSleep: Int?
            let efficiency: Int?
            let latency: Int?
            let remSleep: Int?
            let restfulness: Int?
            let timing: Int?
            let totalSleep: Int?
        }
    }

    /// Overnight blood oxygen, and how disturbed the night's breathing was.
    ///
    /// The two are independently absent — Oura files records carrying one without the other — so
    /// neither stands in for the other's presence.
    struct Spo2: Equatable, Sendable {
        let averagePercentage: Double?
        let breathingDisturbanceIndex: Int?
    }

    /// How much of the day the ring read as stressed and how much as recovered, plus Oura's
    /// one-word verdict on the balance.
    struct Stress: Equatable, Sendable {
        let stressHighSeconds: Int?
        let recoveryHighSeconds: Int?
        /// Oura's own vocabulary, kept as the string it sent: it is an external set Oura extends
        /// on its own schedule, and a closed enum would reject a day the first time it does.
        let daySummary: String?
    }

    /// Oura's resilience level and the three recovery components behind it.
    struct Resilience: Equatable, Sendable {
        let level: String
        let sleepRecovery: Double?
        let daytimeRecovery: Double?
        let stress: Double?
    }
}

extension OuraDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let date = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "An Oura day without a date cannot be placed on a timeline."))
        }

        self.init(
            date: date,
            activity: container.lenient(Activity.self, "activity"),
            readiness: container.lenient(Readiness.self, "readiness"),
            sleep: container.lenient(Sleep.self, "sleep"),
            spo2: container.lenient(Spo2.self, "spo2"),
            stress: container.lenient(Stress.self, "stress"),
            resilience: container.lenient(Resilience.self, "resilience"))
    }
}

extension OuraDay.Activity: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(Int.self, "score"),
            steps: container.lenient("steps", or: 0),
            activeCalories: container.lenient("activeCalories", or: 0),
            totalCalories: container.lenient("totalCalories", or: 0),
            targetCalories: container.lenient("targetCalories", or: 0),
            equivalentWalkingDistanceMeters: container.lenient("equivalentWalkingDistanceMeters", or: 0),
            inactivityAlerts: container.lenient("inactivityAlerts", or: 0),
            averageMet: container.lenient("averageMet", or: 0),
            lowActivityMetMinutes: container.lenient("lowActivityMetMinutes", or: 0),
            mediumActivityMetMinutes: container.lenient("mediumActivityMetMinutes", or: 0),
            highActivityMetMinutes: container.lenient("highActivityMetMinutes", or: 0),
            sedentaryMetMinutes: container.lenient("sedentaryMetMinutes", or: 0),
            restingTimeSeconds: container.lenient("restingTimeSeconds", or: 0),
            sedentaryTimeSeconds: container.lenient("sedentaryTimeSeconds", or: 0),
            lowActivityTimeSeconds: container.lenient("lowActivityTimeSeconds", or: 0),
            mediumActivityTimeSeconds: container.lenient("mediumActivityTimeSeconds", or: 0),
            highActivityTimeSeconds: container.lenient("highActivityTimeSeconds", or: 0),
            nonWearTimeSeconds: container.lenient("nonWearTimeSeconds", or: 0),
            contributors: container.lenient(Contributors.self, "contributors") ?? .absent)
    }
}

extension OuraDay.Activity.Contributors: Decodable {
    static let absent = Self(
        meetDailyTargets: nil, moveEveryHour: nil, recoveryTime: nil,
        stayActive: nil, trainingFrequency: nil, trainingVolume: nil)

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            meetDailyTargets: container.lenient(Int.self, "meetDailyTargets"),
            moveEveryHour: container.lenient(Int.self, "moveEveryHour"),
            recoveryTime: container.lenient(Int.self, "recoveryTime"),
            stayActive: container.lenient(Int.self, "stayActive"),
            trainingFrequency: container.lenient(Int.self, "trainingFrequency"),
            trainingVolume: container.lenient(Int.self, "trainingVolume"))
    }
}

extension OuraDay.Readiness: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(Int.self, "score"),
            temperatureDeviation: container.lenient(Double.self, "temperatureDeviation"),
            temperatureTrendDeviation: container.lenient(Double.self, "temperatureTrendDeviation"),
            contributors: container.lenient(Contributors.self, "contributors") ?? .absent)
    }
}

extension OuraDay.Readiness.Contributors: Decodable {
    static let absent = Self(
        activityBalance: nil, bodyTemperature: nil, hrvBalance: nil, previousDayActivity: nil,
        previousNight: nil, recoveryIndex: nil, restingHeartRate: nil, sleepBalance: nil,
        sleepRegularity: nil)

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            activityBalance: container.lenient(Int.self, "activityBalance"),
            bodyTemperature: container.lenient(Int.self, "bodyTemperature"),
            hrvBalance: container.lenient(Int.self, "hrvBalance"),
            previousDayActivity: container.lenient(Int.self, "previousDayActivity"),
            previousNight: container.lenient(Int.self, "previousNight"),
            recoveryIndex: container.lenient(Int.self, "recoveryIndex"),
            restingHeartRate: container.lenient(Int.self, "restingHeartRate"),
            sleepBalance: container.lenient(Int.self, "sleepBalance"),
            sleepRegularity: container.lenient(Int.self, "sleepRegularity"))
    }
}

extension OuraDay.Sleep: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(Int.self, "score"),
            contributors: container.lenient(Contributors.self, "contributors") ?? .absent)
    }
}

extension OuraDay.Sleep.Contributors: Decodable {
    static let absent = Self(
        deepSleep: nil, efficiency: nil, latency: nil, remSleep: nil,
        restfulness: nil, timing: nil, totalSleep: nil)

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            deepSleep: container.lenient(Int.self, "deepSleep"),
            efficiency: container.lenient(Int.self, "efficiency"),
            latency: container.lenient(Int.self, "latency"),
            remSleep: container.lenient(Int.self, "remSleep"),
            restfulness: container.lenient(Int.self, "restfulness"),
            timing: container.lenient(Int.self, "timing"),
            totalSleep: container.lenient(Int.self, "totalSleep"))
    }
}

extension OuraDay.Spo2: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            averagePercentage: container.lenient(Double.self, "averageSpo2Percentage"),
            breathingDisturbanceIndex: container.lenient(Int.self, "breathingDisturbanceIndex"))
    }
}

extension OuraDay.Stress: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            stressHighSeconds: container.lenient(Int.self, "stressHighSeconds"),
            recoveryHighSeconds: container.lenient(Int.self, "recoveryHighSeconds"),
            daySummary: container.lenient(String.self, "daySummary"))
    }
}

extension OuraDay.Resilience: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let contributors = container.lenient(ResilienceContributorsWire.self, "contributors")
        self.init(
            level: container.lenient("level", or: ""),
            sleepRecovery: contributors?.sleepRecovery,
            daytimeRecovery: contributors?.daytimeRecovery,
            stress: contributors?.stress)
    }
}

/// Resilience's three components, flattened onto the reading.
///
/// Flattened because there are only three of them and no screen asks for them as a group — unlike
/// readiness and sleep, whose contributors are a named nine and seven that a panel lists together.
private struct ResilienceContributorsWire: Decodable {
    let sleepRecovery: Double?
    let daytimeRecovery: Double?
    let stress: Double?

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        sleepRecovery = container.lenient(Double.self, "sleepRecovery")
        daytimeRecovery = container.lenient(Double.self, "daytimeRecovery")
        stress = container.lenient(Double.self, "stress")
    }
}
