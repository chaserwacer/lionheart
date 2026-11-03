import Foundation
import Testing

@testable import Lionheart

/// Builds the ring's days and nights as values, so a test states only the figures it is about.
enum ARecovery {
    static func day(
        _ date: CalendarDay,
        readiness: Int? = 80,
        sleepScore: Int? = 75,
        temperature: Double? = nil,
        steps: Int? = nil
    ) -> OuraDay {
        OuraDay(
            date: date,
            activity: steps.map { activity(steps: $0) },
            readiness: readiness.map {
                OuraDay.Readiness(
                    score: $0, temperatureDeviation: temperature, temperatureTrendDeviation: nil,
                    contributors: .absent)
            },
            sleep: sleepScore.map { OuraDay.Sleep(score: $0, contributors: .absent) },
            spo2: nil, stress: nil, resilience: nil)
    }

    static func night(
        _ date: CalendarDay, hrv: Int? = 60, lowestHeartRate: Int? = 50, asleepSeconds: Int = 27_000,
        isMainSleep: Bool = true
    ) -> OuraEvent<OuraSleepPeriod> {
        OuraEvent(
            ouraId: UUID(),
            day: date,
            period: .unrecognised(""),
            reading: OuraSleepPeriod(
                kind: isMainSleep ? "long_sleep" : "late_nap",
                timeInBedSeconds: asleepSeconds + 1_800, totalSleepSeconds: asleepSeconds,
                deepSleepSeconds: 0, lightSleepSeconds: 0, remSleepSeconds: 0, awakeSeconds: 0,
                latencySeconds: 0, efficiency: 90, restlessPeriods: 0,
                averageHeartRate: nil, lowestHeartRate: lowestHeartRate, averageHrv: hrv,
                averageBreath: 14.5, readinessScoreDelta: nil, sleepScoreDelta: nil, hypnogram: "",
                heartRate: OuraSampleSeries(intervalSeconds: 300, samples: [], startedAt: nil),
                hrv: OuraSampleSeries(intervalSeconds: 300, samples: [], startedAt: nil),
                isMainSleep: isMainSleep))
    }

    private static func activity(steps: Int) -> OuraDay.Activity {
        OuraDay.Activity(
            score: nil, steps: steps, activeCalories: 0, totalCalories: 0, targetCalories: 0,
            equivalentWalkingDistanceMeters: 0, inactivityAlerts: 0, averageMet: 0,
            lowActivityMetMinutes: 0, mediumActivityMetMinutes: 0, highActivityMetMinutes: 0,
            sedentaryMetMinutes: 0, restingTimeSeconds: 0, sedentaryTimeSeconds: 0,
            lowActivityTimeSeconds: 0, mediumActivityTimeSeconds: 0, highActivityTimeSeconds: 0,
            nonWearTimeSeconds: 0, contributors: .absent)
    }

    /// The `count` days before `day`, oldest first.
    static func daysBefore(_ day: CalendarDay, count: Int) -> [CalendarDay] {
        (1...count).reversed().map { day.adding(days: -$0) }
    }
}

@Suite("Recovery against the athlete's baseline")
struct RecoveryTests {
    private static let day = CalendarDay(year: 2026, month: 8, day: 1)

    // MARK: - Baseline

    @Test("Fewer than a week of earlier readings is not a baseline")
    func tooFewDaysIsNoBaseline() {
        #expect(MetricBaseline.of([60, 62, 61, 59, 60, 63]) == nil)
        #expect(MetricBaseline.of([60, 62, 61, 59, 60, 63, 61])?.sampleCount == 7)
    }

    @Test("The baseline is the mean and sample spread of the earlier days")
    func baselineIsMeanAndSpread() throws {
        let baseline = try #require(MetricBaseline.of([2, 4, 4, 4, 5, 5, 7, 9]))

        #expect(baseline.mean == 5)
        #expect(abs(baseline.standardDeviation - 2.138) < 0.001)
    }

    @Test(
        "Outside one spread, the direction that is better depends on the figure",
        arguments: [
            (RecoveryMetric.hrv, 80.0, MetricStanding.favourable),
            (.hrv, 40, .unfavourable),
            (.lowestHeartRate, 80, .unfavourable),
            (.lowestHeartRate, 40, .favourable),
            (.hrv, 61, .typical),
        ])
    func standingFollowsPolarity(metric: RecoveryMetric, value: Double, expected: MetricStanding) throws {
        let baseline = try #require(MetricBaseline.of([58, 60, 62, 60, 58, 62, 60]))

        #expect(baseline.standing(of: value, polarity: metric.polarity) == expected)
    }

    // MARK: - A day's reading

    @Test("A day's figure is compared with the 28 days before it, never with itself")
    func theDayIsNotInItsOwnBaseline() throws {
        let earlier = ARecovery.daysBefore(Self.day, count: 10)
        let nights = earlier.map { ARecovery.night($0, hrv: 60) } + [ARecovery.night(Self.day, hrv: 30)]
        let days = (earlier + [Self.day]).map { ARecovery.day($0) }

        let reading = try #require(RecoveryReading.reading(for: Self.day, days: days, nights: nights))
        let hrv = try #require(reading.figures.first { $0.metric == .hrv })

        #expect(hrv.value == 30)
        #expect(hrv.baseline?.mean == 60)
        #expect(hrv.deviation == -30)
        #expect(hrv.recent.count == 11)
    }

    @Test("The night's main sleep is read, not a longer nap on the same day")
    func theMainSleepIsTheNight() throws {
        let nights = [
            ARecovery.night(Self.day, hrv: 90, asleepSeconds: 30_000, isMainSleep: false),
            ARecovery.night(Self.day, hrv: 55, asleepSeconds: 20_000, isMainSleep: true),
        ]

        let reading = try #require(RecoveryReading.reading(for: Self.day, days: [], nights: nights))

        #expect(reading.figures.first { $0.metric == .hrv }?.value == 55)
        #expect(reading.figures.first { $0.metric == .sleepDuration }?.value == 20_000)
    }

    @Test("A lowest heart rate of zero is a period with no reading, not a heart rate")
    func aZeroHeartRateIsAbsent() throws {
        let reading = try #require(RecoveryReading.reading(
            for: Self.day, days: [], nights: [ARecovery.night(Self.day, lowestHeartRate: 0)]))

        #expect(!reading.figures.contains { $0.metric == .lowestHeartRate })
    }

    @Test("Temperature is already a deviation, so it carries no baseline of its own")
    func temperatureHasNoBaseline() throws {
        let earlier = ARecovery.daysBefore(Self.day, count: 10)
        let days = (earlier + [Self.day]).map { ARecovery.day($0, temperature: 0.2) }

        let reading = try #require(RecoveryReading.reading(for: Self.day, days: days, nights: []))
        let temperature = try #require(reading.figures.first { $0.metric == .temperatureDeviation })

        #expect(temperature.baseline == nil)
        #expect(temperature.standing == .noBaseline)
    }

    @Test("A day the ring filed nothing for has no reading")
    func anEmptyDayHasNoReading() {
        let yesterday = Self.day.adding(days: -1)

        #expect(RecoveryReading.reading(
            for: Self.day, days: [ARecovery.day(yesterday)], nights: [ARecovery.night(yesterday)]) == nil)
    }

    @Test("Readiness leads as the verdict, with its own comparison, and is not repeated as a figure")
    func readinessIsTheVerdict() throws {
        let earlier = ARecovery.daysBefore(Self.day, count: 8)
        let days = earlier.map { ARecovery.day($0, readiness: 70) } + [ARecovery.day(Self.day, readiness: 85)]

        let reading = try #require(RecoveryReading.reading(for: Self.day, days: days, nights: []))

        #expect(reading.readiness?.score == 85)
        #expect(reading.readinessFigure?.deviation == 15)
        #expect(!reading.figures.contains { $0.metric == .readiness })
    }

    // MARK: - Trends

    @Test("A trend's line is a trailing mean drawn only where four of the seven days carry the figure")
    func rollingMeanNeedsFourOfSeven() {
        let start = CalendarDay(year: 2026, month: 7, day: 1)
        let points = [0, 1, 2, 3, 10].map { MetricPoint(day: start.adding(days: $0), value: Double($0)) }

        let mean = MetricTrend.rollingMean(of: points)

        // Day 3 is the fourth point; day 10's window (days 4–10) holds only itself.
        #expect(mean.map(\.day) == [start.adding(days: 3)])
        #expect(mean.first?.value == 1.5)
    }

    @Test("A trend keeps only the days inside its span that carry the figure")
    func trendKeepsItsSpan() {
        let end = CalendarDay(year: 2026, month: 8, day: 1)
        let key = MetricTrendKey(metric: .readiness, span: .month, endingOn: end)
        let days = [
            ARecovery.day(end.adding(days: -30), readiness: 50),
            ARecovery.day(end.adding(days: -29), readiness: 60),
            ARecovery.day(end.adding(days: -1), readiness: nil),
            ARecovery.day(end, readiness: 80),
        ]

        let trend = MetricTrend.reading(key, days: days, nights: [])

        #expect(trend.points.map(\.value) == [60, 80])
        #expect(trend.mean == 70)
    }

    // MARK: - Words

    @Test(
        "A deviation is signed with a true minus, and nothing reads as ±0",
        arguments: [
            (RecoveryMetric.hrv, 6.4, "+6"),
            (.lowestHeartRate, -2.0, "−2"),
            (.hrv, 0.3, "±0"),
            (.sleepDuration, -1_440.0, "−0:24"),
            (.sleepDuration, 3_900.0, "+1:05"),
            (.respiratoryRate, 0.04, "±0"),
        ])
    func deviationWords(metric: RecoveryMetric, value: Double, expected: String) {
        #expect(Formatting.deviation(value, of: metric) == expected)
    }
}
