import Foundation

/// One figure the athlete reads his recovery by, and where it comes from.
///
/// A closed set rather than a list of panel rows because each one has to say three things a row
/// cannot: which record it is read from (the day's documents or the night's main sleep), which
/// direction is good, and whether a comparison against his own baseline means anything.
///
/// HRV, lowest heart rate, breathing and sleep duration are **measurements** from the night's main
/// sleep period. The readiness contributors called "HRV balance" and "resting heart rate" are
/// Oura's scores comparing a day against his baseline; they are not used here, because the point of
/// these figures is to show the measurement and make the comparison visible.
enum RecoveryMetric: String, CaseIterable, Hashable, Sendable, Identifiable {
    case readiness
    case hrv
    case lowestHeartRate
    case sleepDuration
    case sleepScore
    case respiratoryRate
    case temperatureDeviation
    case steps

    var id: String { rawValue }

    /// Which direction of change is the better one.
    enum Polarity: Equatable, Sendable {
        case higherIsBetter
        case lowerIsBetter
    }

    var polarity: Polarity {
        switch self {
        case .readiness, .hrv, .sleepDuration, .sleepScore, .steps: .higherIsBetter
        case .lowestHeartRate, .respiratoryRate: .lowerIsBetter
        // Not compared to a baseline (see `comparesToBaseline`); the polarity is never consulted.
        case .temperatureDeviation: .lowerIsBetter
        }
    }

    /// Whether a deviation from his own baseline is worth showing.
    ///
    /// Temperature is already Oura's deviation from his baseline, so subtracting a second baseline
    /// from it would be a deviation of a deviation.
    var comparesToBaseline: Bool {
        switch self {
        case .temperatureDeviation: false
        case .readiness, .hrv, .lowestHeartRate, .sleepDuration, .sleepScore, .respiratoryRate, .steps: true
        }
    }

    /// Whether the figure keeps growing until the day ends. Today's steps at breakfast are not a
    /// low day, so a figure like this is neither compared with a baseline nor charted until its day
    /// is over.
    var accruesThroughTheDay: Bool {
        switch self {
        case .steps: true
        case .readiness, .hrv, .lowestHeartRate, .sleepDuration, .sleepScore, .respiratoryRate,
             .temperatureDeviation: false
        }
    }

    /// Whether the figure is one of Oura's 0–100 scores rather than a measurement. Such a figure is
    /// labelled as Oura's wherever it is shown beside measured ones.
    var isOuraScore: Bool {
        switch self {
        case .readiness, .sleepScore: true
        case .hrv, .lowestHeartRate, .sleepDuration, .respiratoryRate, .temperatureDeviation, .steps: false
        }
    }

    /// Whether the figure is read from the night rather than the day's documents — which decides
    /// whether a trend of it needs the sleep periods read.
    var isReadFromTheNight: Bool {
        switch self {
        case .hrv, .lowestHeartRate, .sleepDuration, .respiratoryRate: true
        case .readiness, .sleepScore, .temperatureDeviation, .steps: false
        }
    }

    /// The figure on one day, or `nil` where the ring filed nothing for it.
    ///
    /// A night's lowest heart rate of zero is a period the ring took no reading across, not a heart
    /// rate, so it reads as absent — the same rule the night's own screen applies.
    func value(day: OuraDay?, night: OuraSleepPeriod?) -> Double? {
        switch self {
        case .readiness: day?.readiness?.score?.value
        case .sleepScore: day?.sleep?.score?.value
        case .temperatureDeviation: day?.readiness?.temperatureDeviation
        case .steps: day?.activity.map { Double($0.steps) }
        case .hrv: night?.averageHrv.map(Double.init)
        case .lowestHeartRate: night?.lowestHeartRate.flatMap { $0 > 0 ? Double($0) : nil }
        case .respiratoryRate: night?.averageBreath
        case .sleepDuration: night.flatMap { $0.totalSleepSeconds > 0 ? Double($0.totalSleepSeconds) : nil }
        }
    }
}

extension OuraSleepPeriod {
    /// The night's main sleep among a day's periods, where Oura scored one.
    ///
    /// A day can hold several sleep periods — naps and stretches of rest among them — and only one
    /// is the night. Taking the longest instead would pick a long afternoon nap over a broken
    /// night, which is why Oura's own flag decides it.
    static func mainSleep(on day: CalendarDay, in periods: [OuraEvent<OuraSleepPeriod>]) -> OuraSleepPeriod? {
        periods.first { $0.day == day && $0.reading.isMainSleep }?.reading
    }
}
