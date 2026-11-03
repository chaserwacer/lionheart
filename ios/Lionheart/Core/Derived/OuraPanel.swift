import Foundation

/// One collection of an Oura day, named and reduced to the figures a screen shows.
///
/// A case per collection rather than one struct of optionals, because the collections arrive
/// **independently** — a day can carry sleep and no readiness, and Oura files them separately — so
/// "a readiness with nothing in it" is not a state that needs representing. The screen draws the
/// panels a day has, so a collection Oura adds becomes a case and a section here rather than a
/// redesign of either.
enum OuraPanel: Equatable, Sendable, Identifiable {
    case readiness(OuraDay.Readiness)
    case sleep(OuraDay.Sleep)
    case activity(OuraDay.Activity)
    case stress(OuraDay.Stress)
    case bloodOxygen(OuraDay.Spo2)
    case resilience(OuraDay.Resilience)
    case vo2Max(OuraDay.Vo2Max)
    case cardiovascularAge(OuraDay.CardiovascularAge)

    var id: String { title }

    var title: String {
        switch self {
        case .readiness: "Readiness"
        case .sleep: "Sleep"
        case .activity: "Activity"
        case .stress: "Stress"
        case .bloodOxygen: "Blood oxygen"
        case .resilience: "Resilience"
        case .vo2Max: "VO₂max"
        case .cardiovascularAge: "Cardiovascular age"
        }
    }

    /// Oura's own score for the collection, where it issues one. Stress, blood oxygen and
    /// resilience have none — they have measurements and a verdict — which is why this is optional
    /// rather than a zero standing in for "no score".
    var score: Int? {
        switch self {
        case .readiness(let readiness): readiness.score
        case .sleep(let sleep): sleep.score
        case .activity(let activity): activity.score
        case .stress, .bloodOxygen, .resilience, .vo2Max, .cardiovascularAge: nil
        }
    }

    /// One labelled figure in a panel.
    struct Figure: Equatable, Sendable, Identifiable {
        let label: String
        let value: String

        var id: String { label }
    }

    /// What this panel shows besides its score.
    ///
    /// Exhaustive over the panels, so a collection added as the Oura surface widens has to say
    /// what it shows rather than silently rendering as a bare score. A figure Oura did not send is
    /// left out rather than printed as a dash: the panel is the set of things that were measured.
    var figures: [Figure] {
        switch self {
        case .readiness(let readiness):
            // Contributors are scores out of 100 comparing the day against the athlete's own
            // baseline — "resting heart rate" here is a balance, never a rate. The rate is on the
            // night, in `OuraSleepPeriod.lowestHeartRate`.
            let contributors = readiness.contributors
            return [
                Self.figure("HRV balance", contributors.hrvBalance),
                Self.figure("Resting HR balance", contributors.restingHeartRate),
                Self.figure("Recovery index", contributors.recoveryIndex),
                Self.figure("Sleep balance", contributors.sleepBalance),
                Self.figure("Sleep regularity", contributors.sleepRegularity),
                Self.figure("Previous day's activity", contributors.previousDayActivity),
                Self.figure("Body temperature", contributors.bodyTemperature),
                readiness.temperatureDeviation.map {
                    Figure(label: "Temperature deviation", value: String(format: "%+.2f °C", $0))
                },
            ].compactMap(\.self)

        case .sleep(let sleep):
            let contributors = sleep.contributors
            return [
                Self.figure("Total sleep", contributors.totalSleep),
                Self.figure("Efficiency", contributors.efficiency),
                Self.figure("Restfulness", contributors.restfulness),
                Self.figure("REM sleep", contributors.remSleep),
                Self.figure("Deep sleep", contributors.deepSleep),
                Self.figure("Latency", contributors.latency),
                Self.figure("Timing", contributors.timing),
            ].compactMap(\.self)

        case .activity(let activity):
            return [
                Figure(label: "Steps", value: "\(activity.steps)"),
                Figure(label: "Active calories", value: "\(activity.activeCalories) kcal"),
                Figure(label: "Total calories", value: "\(activity.totalCalories) kcal"),
                Figure(
                    label: "Walking equivalent",
                    value: MeasurementText.plain(
                        Double(activity.equivalentWalkingDistanceMeters) / 1000) + " km"),
                Figure(label: "Average MET", value: MeasurementText.plain(activity.averageMet)),
                Figure(
                    label: "High activity",
                    value: MeasurementText.spelledDuration(
                        seconds: Double(activity.highActivityTimeSeconds))),
                Figure(
                    label: "Sedentary",
                    value: MeasurementText.spelledDuration(
                        seconds: Double(activity.sedentaryTimeSeconds))),
                Figure(
                    label: "Not worn",
                    value: MeasurementText.spelledDuration(
                        seconds: Double(activity.nonWearTimeSeconds))),
            ]

        case .stress(let stress):
            return [
                stress.daySummary?.nonEmpty.map {
                    Figure(label: "Oura's read", value: $0.replacingOccurrences(of: "_", with: " "))
                },
                stress.stressHighSeconds.map {
                    Figure(
                        label: "Stressed",
                        value: MeasurementText.spelledDuration(seconds: Double($0)))
                },
                stress.recoveryHighSeconds.map {
                    Figure(
                        label: "Recovered",
                        value: MeasurementText.spelledDuration(seconds: Double($0)))
                },
            ].compactMap(\.self)

        case .bloodOxygen(let spo2):
            // The two are independently absent, so neither stands in for the other's presence.
            return [
                spo2.averagePercentage.map {
                    Figure(label: "Average SpO₂", value: MeasurementText.plain($0) + "%")
                },
                spo2.breathingDisturbanceIndex.map {
                    Figure(label: "Breathing disturbance", value: "\($0)")
                },
            ].compactMap(\.self)

        case .resilience(let resilience):
            return [
                resilience.level.nonEmpty.map {
                    Figure(label: "Level", value: $0.replacingOccurrences(of: "_", with: " "))
                },
                resilience.sleepRecovery.map {
                    Figure(label: "Sleep recovery", value: MeasurementText.plain($0))
                },
                resilience.daytimeRecovery.map {
                    Figure(label: "Daytime recovery", value: MeasurementText.plain($0))
                },
                resilience.stress.map {
                    Figure(label: "Stress", value: MeasurementText.plain($0))
                },
            ].compactMap(\.self)

        case .vo2Max(let estimate):
            // Oura's estimate from heart rate and walking, not a test; the label says so.
            return [Figure(label: "Estimate, ml/kg/min", value: "\(estimate.value)")]

        case .cardiovascularAge(let age):
            return [
                age.vascularAge.map { Figure(label: "Predicted, years", value: "\($0)") },
                age.pulseWaveVelocity.map {
                    Figure(label: "Pulse-wave velocity", value: MeasurementText.plain($0) + " m/s")
                },
            ].compactMap(\.self)
        }
    }

    /// A contributor score out of 100, or nothing where Oura sent none.
    private static func figure(_ label: String, _ score: Int?) -> Figure? {
        score.map { Figure(label: label, value: "\($0)") }
    }

    /// The panels a day actually carries, in the order the screen reads them: how recovered, how
    /// you slept, what you did, how hard the day was, and the two Oura only sometimes files.
    static func panels(of day: OuraDay) -> [OuraPanel] {
        var panels: [OuraPanel] = []
        if let readiness = day.readiness { panels.append(.readiness(readiness)) }
        if let sleep = day.sleep { panels.append(.sleep(sleep)) }
        if let activity = day.activity { panels.append(.activity(activity)) }
        if let stress = day.stress { panels.append(.stress(stress)) }
        if let spo2 = day.spo2 { panels.append(.bloodOxygen(spo2)) }
        if let resilience = day.resilience { panels.append(.resilience(resilience)) }
        if let vo2Max = day.vo2Max { panels.append(.vo2Max(vo2Max)) }
        if let age = day.cardiovascularAge { panels.append(.cardiovascularAge(age)) }
        return panels
    }
}
