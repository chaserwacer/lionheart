import Foundation

/// One of the components Oura builds a score from — readiness, sleep, activity, resilience.
///
/// Every one is a score from 0 to 100 comparing the day against the athlete's own baseline, and for
/// every one higher is better: Oura orients them all that way, including the resilience component
/// it calls "stress". None is a measurement — "resting heart rate" here is how his resting rate
/// compared, never a rate.
enum Contributor: String, CaseIterable, Hashable, Sendable, Identifiable {
    // Readiness
    case hrvBalance, restingHeartRate, recoveryIndex, sleepBalance, previousNight, sleepRegularity
    case activityBalance, previousDayActivity, bodyTemperature
    // Sleep
    case totalSleep, efficiency, restfulness, remSleep, deepSleep, latency, timing
    // Activity
    case trainingFrequency, trainingVolume, recoveryTime, stayActive, moveEveryHour, meetDailyTargets
    // Resilience
    case sleepRecovery, daytimeRecovery, stress

    var id: String { rawValue }

    var group: ContributorGroup {
        switch self {
        case .hrvBalance, .restingHeartRate, .recoveryIndex, .sleepBalance, .previousNight, .sleepRegularity,
             .activityBalance, .previousDayActivity, .bodyTemperature: .readiness
        case .totalSleep, .efficiency, .restfulness, .remSleep, .deepSleep, .latency, .timing: .sleep
        case .trainingFrequency, .trainingVolume, .recoveryTime, .stayActive, .moveEveryHour, .meetDailyTargets: .activity
        case .sleepRecovery, .daytimeRecovery, .stress: .resilience
        }
    }

    /// Oura's own name for the component.
    var name: String {
        switch self {
        case .hrvBalance: "HRV balance"
        case .restingHeartRate: "Resting HR"
        case .recoveryIndex: "Recovery index"
        case .sleepBalance: "Sleep balance"
        case .previousNight: "Previous night"
        case .sleepRegularity: "Sleep regularity"
        case .activityBalance: "Activity balance"
        case .previousDayActivity: "Previous day"
        case .bodyTemperature: "Body temperature"
        case .totalSleep: "Total sleep"
        case .efficiency: "Efficiency"
        case .restfulness: "Restfulness"
        case .remSleep: "REM sleep"
        case .deepSleep: "Deep sleep"
        case .latency: "Latency"
        case .timing: "Timing"
        case .trainingFrequency: "Training frequency"
        case .trainingVolume: "Training volume"
        case .recoveryTime: "Recovery time"
        case .stayActive: "Stay active"
        case .moveEveryHour: "Move every hour"
        case .meetDailyTargets: "Meet daily targets"
        case .sleepRecovery: "Sleep recovery"
        case .daytimeRecovery: "Daytime recovery"
        case .stress: "Stress"
        }
    }

    /// The component's score on a day, or `nil` where Oura sent none.
    func value(on day: OuraDay) -> Double? {
        let readiness = day.readiness?.contributors
        let sleep = day.sleep?.contributors
        let activity = day.activity?.contributors
        let score: Int? = switch self {
        case .hrvBalance: readiness?.hrvBalance
        case .restingHeartRate: readiness?.restingHeartRate
        case .recoveryIndex: readiness?.recoveryIndex
        case .sleepBalance: readiness?.sleepBalance
        case .previousNight: readiness?.previousNight
        case .sleepRegularity: readiness?.sleepRegularity
        case .activityBalance: readiness?.activityBalance
        case .previousDayActivity: readiness?.previousDayActivity
        case .bodyTemperature: readiness?.bodyTemperature
        case .totalSleep: sleep?.totalSleep
        case .efficiency: sleep?.efficiency
        case .restfulness: sleep?.restfulness
        case .remSleep: sleep?.remSleep
        case .deepSleep: sleep?.deepSleep
        case .latency: sleep?.latency
        case .timing: sleep?.timing
        case .trainingFrequency: activity?.trainingFrequency
        case .trainingVolume: activity?.trainingVolume
        case .recoveryTime: activity?.recoveryTime
        case .stayActive: activity?.stayActive
        case .moveEveryHour: activity?.moveEveryHour
        case .meetDailyTargets: activity?.meetDailyTargets
        // Resilience's components arrive as decimals rather than whole scores; read below.
        case .sleepRecovery, .daytimeRecovery, .stress: nil
        }
        if let score { return Double(score) }

        let resilience = day.resilience
        return switch self {
        case .sleepRecovery: resilience?.sleepRecovery
        case .daytimeRecovery: resilience?.daytimeRecovery
        case .stress: resilience?.stress
        case .hrvBalance, .restingHeartRate, .recoveryIndex, .sleepBalance, .previousNight, .sleepRegularity,
             .activityBalance, .previousDayActivity, .bodyTemperature, .totalSleep, .efficiency, .restfulness,
             .remSleep, .deepSleep, .latency, .timing, .trainingFrequency, .trainingVolume, .recoveryTime,
             .stayActive, .moveEveryHour, .meetDailyTargets: nil
        }
    }
}

/// The four scores Oura builds from contributors, in the order a recovery is read.
enum ContributorGroup: CaseIterable, Hashable, Sendable, Identifiable {
    case readiness, sleep, activity, resilience

    var id: Self { self }

    var title: String {
        switch self {
        case .readiness: "Readiness"
        case .sleep: "Sleep"
        case .activity: "Activity"
        case .resilience: "Resilience"
        }
    }

    /// The group's components, in the order the screen reads them: what moves the score most first.
    var contributors: [Contributor] { Contributor.allCases.filter { $0.group == self } }

    /// Oura's own score for the group on the day, where it issues one. Resilience has a level, not
    /// a score.
    func score(on day: OuraDay) -> Int? {
        switch self {
        case .readiness: day.readiness?.score
        case .sleep: day.sleep?.score
        case .activity: day.activity?.score
        case .resilience: nil
        }
    }
}

/// One contributor on one day, against the same contributor over the four weeks before.
struct ContributorReading: Equatable, Sendable, Identifiable {
    let contributor: Contributor
    let value: Double
    /// `nil` when fewer than a week of the earlier days carry it.
    let baseline: MetricBaseline?

    var id: Contributor { contributor }

    /// Every contributor is higher-is-better, so the standing is the recovery figures' rule with
    /// that polarity.
    var standing: MetricStanding {
        baseline?.standing(of: value, polarity: .higherIsBetter) ?? .noBaseline
    }

    /// The band the athlete usually sits in — one spread either side of his mean — clipped to the
    /// 0–100 scale the scores live on.
    var usualRange: ClosedRange<Double>? {
        guard let baseline else { return nil }
        let low = max(0, baseline.mean - baseline.standardDeviation)
        let high = min(100, baseline.mean + baseline.standardDeviation)
        return low...max(low, high)
    }

    /// The group's contributors on `day`, each against the `MetricBaseline.windowDays` days before
    /// it in `days`. A contributor Oura sent nothing for that day is left out.
    static func readings(
        of group: ContributorGroup, on day: OuraDay, among days: [OuraDay], calendar: Calendar = .current
    ) -> [ContributorReading] {
        let windowStart = day.date.adding(days: -MetricBaseline.windowDays, in: calendar)
        let earlier = days.filter { $0.date >= windowStart && $0.date < day.date }

        return group.contributors.compactMap { contributor in
            contributor.value(on: day).map { value in
                ContributorReading(
                    contributor: contributor,
                    value: value,
                    baseline: MetricBaseline.of(earlier.compactMap(contributor.value(on:))))
            }
        }
    }
}
