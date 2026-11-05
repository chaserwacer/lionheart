import Foundation

/// One of the readiness contributors Today picks out, beside the measurement it rests on.
///
/// Oura's contributor is a 0–100 score: its rating of something against the athlete's baseline. The
/// measurement under it is what actually happened — last night's HRV, the night's lowest heart
/// rate and when it came, how long he slept over the last fortnight — and Lionheart holds it for
/// most of them. Putting the two side by side is the point: the score says how Oura judged it, the
/// figure says what there was to judge.
///
/// Six of Oura's nine, the ones that answer "how recovered am I" from the night: the nervous system
/// (HRV balance, resting heart rate, recovery index), sleep (sleep balance, previous night), and
/// temperature. Activity balance, previous day's activity and sleep regularity rate the day before
/// and the routine rather than the recovery, and are on the day's own screen in Body.
struct ReadinessContributorReading: Equatable, Sendable, Identifiable {
    let contributor: OuraContributor
    /// Oura's score with his usual band of it, or `nil` where Oura issued none for the day.
    let oura: OuraContributorReading?
    /// What Lionheart measured that the score rates, or `nil` where it has no measurement.
    let basis: Basis?

    var id: OuraContributor { contributor }

    /// Whether the measurement behind the score is the night's — so its absence means the night
    /// has not reached the ring's data yet, which Oura often files a readiness before.
    var restsOnTheNight: Bool {
        switch contributor {
        case .hrvBalance, .restingHeartRate, .recoveryIndex, .previousNight: true
        case .sleepBalance, .bodyTemperature, .sleepRegularity, .activityBalance, .previousDayActivity,
             .totalSleep, .efficiency, .restfulness, .remSleep, .deepSleep, .latency, .timing,
             .trainingFrequency, .trainingVolume, .recoveryTime, .stayActive, .moveEveryHour,
             .meetDailyTargets, .sleepRecovery, .daytimeRecovery, .stress:
            false
        }
    }

    /// Whether the row asks for attention: Oura's score, or the measurement under it, outside his
    /// usual range in the worse direction — the row's orange.
    var isUnusual: Bool {
        if oura?.standing == .unfavourable { return true }
        if case .measured(let figure) = basis { return figure.standing == .unfavourable }
        return false
    }

    /// The measurement behind a contributor.
    enum Basis: Equatable, Sendable {
        /// A recovery figure against his own 28 days before, which opens its trend.
        case measured(MetricReading)
        /// When in the night the heart rate bottomed out — what Oura's recovery index rates, the
        /// earlier the better.
        case lowestHeartRate(LowestHeartRateTiming)
        /// How much he slept over the last fortnight — what sleep balance rates.
        case recentSleep(RecentSleep)
    }

    /// The contributors Today shows, in the order it reads them.
    static let picked: [OuraContributor] = [
        .hrvBalance, .restingHeartRate, .recoveryIndex, .sleepBalance, .previousNight, .bodyTemperature,
    ]

    /// Reads the picked contributors for the recovery's day.
    ///
    /// `days` and `nights` reach back at least `MetricBaseline.windowDays` before it, which is what
    /// Today already reads; a contributor with neither a score nor a measurement is left out.
    static func readings(
        for recovery: RecoveryReading,
        days: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>],
        calendar: Calendar = .current
    ) -> [ReadinessContributorReading] {
        let day = days.first { $0.date == recovery.day }
        let night = nights.first { $0.day == recovery.day && $0.reading.isMainSleep }

        return picked.compactMap { contributor in
            let oura = day.flatMap { OuraContributorReading.reading(of: contributor, on: $0, among: days, calendar: calendar) }
            let basis = basis(for: contributor, recovery: recovery, night: night, nights: nights, calendar: calendar)
            guard oura != nil || basis != nil else { return nil }
            return ReadinessContributorReading(contributor: contributor, oura: oura, basis: basis)
        }
    }

    private static func basis(
        for contributor: OuraContributor,
        recovery: RecoveryReading,
        night: OuraEvent<OuraSleepPeriod>?,
        nights: [OuraEvent<OuraSleepPeriod>],
        calendar: Calendar
    ) -> Basis? {
        func measured(_ metric: RecoveryMetric) -> Basis? {
            recovery.figures.first { $0.metric == metric }.map(Basis.measured)
        }
        return switch contributor {
        case .hrvBalance: measured(.hrv)
        case .restingHeartRate: measured(.lowestHeartRate)
        case .previousNight: measured(.sleepDuration)
        case .bodyTemperature: measured(.temperatureDeviation)
        case .recoveryIndex: night.flatMap(LowestHeartRateTiming.of).map(Basis.lowestHeartRate)
        case .sleepBalance:
            RecentSleep.of(nights, endingOn: recovery.day, calendar: calendar).map(Basis.recentSleep)
        case .sleepRegularity, .activityBalance, .previousDayActivity, .totalSleep, .efficiency, .restfulness,
             .remSleep, .deepSleep, .latency, .timing, .trainingFrequency, .trainingVolume, .recoveryTime,
             .stayActive, .moveEveryHour, .meetDailyTargets, .sleepRecovery, .daytimeRecovery, .stress:
            nil
        }
    }
}

/// When the night's heart rate reached its lowest, read off the overnight curve.
///
/// Oura's recovery index rates how soon the resting rate settled: a low point in the first half of
/// the night reads as recovered, one near waking as a body still working something off — late
/// training, alcohol, a heavy meal. This is the measurement that rating rests on.
struct LowestHeartRateTiming: Equatable, Sendable {
    let bpm: Double
    let at: Date
    /// How far into the night it came, from when the period began.
    let intoNight: TimeInterval
    /// The same as a share of the night, 0 at bedtime to 1 at waking.
    let shareOfNight: Double

    /// The timing in `night`, or `nil` without a curve or a span to place it in.
    static func of(_ night: OuraEvent<OuraSleepPeriod>) -> LowestHeartRateTiming? {
        guard case .interval(let start, let end) = night.period, end > start,
              let lowest = night.reading.heartRate.measured.filter({ $0.value > 0 }).min(by: { $0.value < $1.value })
        else { return nil }
        let into = lowest.at.timeIntervalSince(start)
        return LowestHeartRateTiming(
            bpm: lowest.value,
            at: lowest.at,
            intoNight: into,
            shareOfNight: min(1, max(0, into / end.timeIntervalSince(start))))
    }
}

/// His sleep across the fortnight ending on a day: what Oura's sleep balance rates against a need
/// it does not publish.
///
/// Time in bed travels with time asleep because the two answer different questions — an efficient
/// sleeper short of sleep is short of time in bed, and the remedy is the bedtime, not the night.
struct RecentSleep: Equatable, Sendable {
    let meanAsleep: TimeInterval
    let meanInBed: TimeInterval
    let nights: Int

    /// The fortnight Oura's sleep balance weighs.
    static let windowNights = 14

    /// The main sleeps filed in the fortnight ending on `day`, or `nil` with none in it.
    static func of(
        _ nights: [OuraEvent<OuraSleepPeriod>], endingOn day: CalendarDay, calendar: Calendar = .current
    ) -> RecentSleep? {
        let first = day.adding(days: 1 - windowNights, in: calendar)
        let main = nights
            .filter { $0.reading.isMainSleep && $0.day >= first && $0.day <= day && $0.reading.totalSleepSeconds > 0 }
        guard !main.isEmpty else { return nil }
        let count = Double(main.count)
        return RecentSleep(
            meanAsleep: Double(main.reduce(0) { $0 + $1.reading.totalSleepSeconds }) / count,
            meanInBed: Double(main.reduce(0) { $0 + $1.reading.timeInBedSeconds }) / count,
            nights: main.count)
    }
}
