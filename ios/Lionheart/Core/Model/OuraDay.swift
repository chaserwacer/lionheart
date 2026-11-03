import Foundation

/// Everything the ring recorded about one calendar day.
///
/// Eight collections, each independently absent. Oura files them separately and the ring is off the
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
    let vo2Max: Vo2Max?
    let cardiovascularAge: CardiovascularAge?

    var id: CalendarDay { date }

    /// Whether the ring filed anything at all for this day.
    var isEmpty: Bool {
        activity == nil && readiness == nil && sleep == nil
            && spo2 == nil && stress == nil && resilience == nil
            && vo2Max == nil && cardiovascularAge == nil
    }

    /// Oura's VO2max estimate, ml/kg/min — an estimate from heart rate and walking, not a test.
    struct Vo2Max: Equatable, Sendable {
        let value: Int
        let estimatedAt: Date?
    }

    /// Oura's predicted vascular age, and the pulse-wave velocity (m/s) it derives from it. A model
    /// output; the two are independently absent.
    struct CardiovascularAge: Equatable, Sendable {
        let vascularAge: Int?
        let pulseWaveVelocity: Double?
    }

    /// How much the athlete moved, and Oura's score for it.
    struct Activity: Equatable, Sendable {
        let score: OuraScore?
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

        /// The six components Oura's activity score is built from — Oura's scores, each 1–100, not
        /// counts: `trainingFrequency` is how Oura rated the frequency, never a number of sessions.
        struct Contributors: Equatable, Sendable {
            let meetDailyTargets: OuraScore?
            let moveEveryHour: OuraScore?
            let recoveryTime: OuraScore?
            let stayActive: OuraScore?
            let trainingFrequency: OuraScore?
            let trainingVolume: OuraScore?
        }
    }

    /// Oura's read on how recovered the body is, and the components behind it.
    struct Readiness: Equatable, Sendable {
        let score: OuraScore?
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
            let activityBalance: OuraScore?
            let bodyTemperature: OuraScore?
            let hrvBalance: OuraScore?
            let previousDayActivity: OuraScore?
            let previousNight: OuraScore?
            let recoveryIndex: OuraScore?
            let restingHeartRate: OuraScore?
            let sleepBalance: OuraScore?
            let sleepRegularity: OuraScore?
        }
    }

    /// Oura's score for the night as a whole, and the components behind it.
    struct Sleep: Equatable, Sendable {
        let score: OuraScore?
        let contributors: Contributors

        /// The seven components Oura's sleep score is built from — Oura's scores, each 1–100, not the
        /// durations they rate.
        struct Contributors: Equatable, Sendable {
            let deepSleep: OuraScore?
            let efficiency: OuraScore?
            let latency: OuraScore?
            let remSleep: OuraScore?
            let restfulness: OuraScore?
            let timing: OuraScore?
            let totalSleep: OuraScore?
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
        let sleepRecovery: OuraScore?
        let daytimeRecovery: OuraScore?
        let stress: OuraScore?
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
            resilience: container.lenient(Resilience.self, "resilience"),
            vo2Max: container.lenient(Vo2Max.self, "vo2Max"),
            cardiovascularAge: container.lenient(CardiovascularAge.self, "cardiovascularAge"))
    }
}

extension OuraDay.Vo2Max: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let value = container.lenient(Int.self, "vo2Max") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A VO2max estimate without its value."))
        }
        self.init(value: value, estimatedAt: container.instant("estimatedAt"))
    }
}

extension OuraDay.CardiovascularAge: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            vascularAge: container.lenient(Int.self, "vascularAge"),
            pulseWaveVelocity: container.lenient(Double.self, "pulseWaveVelocity"))
    }
}

extension OuraDay.Activity: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(OuraScore.self, "score"),
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
            meetDailyTargets: container.lenient(OuraScore.self, "meetDailyTargets"),
            moveEveryHour: container.lenient(OuraScore.self, "moveEveryHour"),
            recoveryTime: container.lenient(OuraScore.self, "recoveryTime"),
            stayActive: container.lenient(OuraScore.self, "stayActive"),
            trainingFrequency: container.lenient(OuraScore.self, "trainingFrequency"),
            trainingVolume: container.lenient(OuraScore.self, "trainingVolume"))
    }
}

extension OuraDay.Readiness: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(OuraScore.self, "score"),
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
            activityBalance: container.lenient(OuraScore.self, "activityBalance"),
            bodyTemperature: container.lenient(OuraScore.self, "bodyTemperature"),
            hrvBalance: container.lenient(OuraScore.self, "hrvBalance"),
            previousDayActivity: container.lenient(OuraScore.self, "previousDayActivity"),
            previousNight: container.lenient(OuraScore.self, "previousNight"),
            recoveryIndex: container.lenient(OuraScore.self, "recoveryIndex"),
            restingHeartRate: container.lenient(OuraScore.self, "restingHeartRate"),
            sleepBalance: container.lenient(OuraScore.self, "sleepBalance"),
            sleepRegularity: container.lenient(OuraScore.self, "sleepRegularity"))
    }
}

extension OuraDay.Sleep: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            score: container.lenient(OuraScore.self, "score"),
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
            deepSleep: container.lenient(OuraScore.self, "deepSleep"),
            efficiency: container.lenient(OuraScore.self, "efficiency"),
            latency: container.lenient(OuraScore.self, "latency"),
            remSleep: container.lenient(OuraScore.self, "remSleep"),
            restfulness: container.lenient(OuraScore.self, "restfulness"),
            timing: container.lenient(OuraScore.self, "timing"),
            totalSleep: container.lenient(OuraScore.self, "totalSleep"))
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
    let sleepRecovery: OuraScore?
    let daytimeRecovery: OuraScore?
    let stress: OuraScore?

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        sleepRecovery = container.lenient(OuraScore.self, "sleepRecovery")
        daytimeRecovery = container.lenient(OuraScore.self, "daytimeRecovery")
        stress = container.lenient(OuraScore.self, "stress")
    }
}
