import Foundation
import Testing

@testable import Lionheart

/// Today's readiness contributors: Oura's scores, each beside the measurement it rests on.
@Suite("Readiness contributors")
struct ReadinessContributorTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)

    @Test("Each picked contributor pairs Oura's score with the measurement it rates")
    func contributorsPairScoreAndMeasurement() throws {
        let days = ARecovery.daysBefore(Self.today, count: 28).map { Self.scored($0, hrvBalance: 70) }
            + [Self.scored(Self.today, hrvBalance: 55)]
        let nights = (ARecovery.daysBefore(Self.today, count: 28) + [Self.today]).map { ARecovery.night($0) }
        let recovery = try #require(RecoveryReading.reading(for: Self.today, days: days, nights: nights, today: Self.today))

        let readings = ReadinessContributorReading.readings(for: recovery, days: days, nights: nights)

        let hrv = try #require(readings.first { $0.contributor == .hrvBalance })
        #expect(hrv.oura?.score == OuraScore(55))
        guard case .measured(let figure) = hrv.basis else { Issue.record("no measurement"); return }
        #expect(figure.metric == .hrv)
        #expect(readings.map(\.contributor) == ReadinessContributorReading.picked)
    }

    @Test("A contributor with a measurement and no score from Oura still shows the measurement")
    func aMeasurementStandsWithoutAScore() throws {
        let days = [ARecovery.day(Self.today)]
        let nights = [ARecovery.night(Self.today)]
        let recovery = try #require(RecoveryReading.reading(for: Self.today, days: days, nights: nights, today: Self.today))

        let readings = ReadinessContributorReading.readings(for: recovery, days: days, nights: nights)

        let resting = try #require(readings.first { $0.contributor == .restingHeartRate })
        #expect(resting.oura == nil)
        #expect(resting.basis != nil)
    }

    @Test("The figures a contributor row already shows are not listed again beneath")
    func coveredFiguresAreNotRepeated() throws {
        let days = [ARecovery.day(Self.today, steps: 9000)]
        let nights = [ARecovery.night(Self.today)]

        let snapshot = TodaySnapshot.reading(sessions: [], ouraDays: days, nights: nights, today: Self.today)

        let others = snapshot.otherFigures.map(\.metric)
        #expect(!others.contains(.hrv))
        #expect(!others.contains(.lowestHeartRate))
        #expect(!others.contains(.sleepDuration))
        #expect(others.contains(.steps))
        #expect(others.contains(.sleepScore))
    }

    @Test("The recovery index rests on when the night's heart rate bottomed out")
    func recoveryIndexTiming() throws {
        let bedtime = Date(timeIntervalSince1970: 1_772_600_000)
        // Eight hours in five-minute samples, lowest a quarter of the way through.
        var samples: [Double?] = Array(repeating: 58, count: 96)
        samples[24] = 47
        samples[25] = nil
        let night = Self.night(start: bedtime, hours: 8, samples: samples)

        let timing = try #require(LowestHeartRateTiming.of(night))

        #expect(timing.bpm == 47)
        #expect(timing.at == bedtime.addingTimeInterval(24 * 300))
        #expect(abs(timing.shareOfNight - 0.25) < 0.001)
    }

    @Test("A night with no curve has no timing to show")
    func noCurveNoTiming() {
        #expect(LowestHeartRateTiming.of(ARecovery.night(Self.today)) == nil)
    }

    @Test("Sleep balance rests on the last fortnight's mean, asleep and in bed")
    func recentSleepIsTheFortnightsMean() throws {
        let nights = (0..<20).map { offset in
            ARecovery.night(Self.today.adding(days: -offset), asleepSeconds: offset < 14 ? 25_200 : 18_000)
        } + [ARecovery.night(Self.today, asleepSeconds: 3_600, isMainSleep: false)]

        let sleep = try #require(RecentSleep.of(nights, endingOn: Self.today))

        #expect(sleep.nights == 14)
        #expect(sleep.meanAsleep == 25_200)
        #expect(sleep.meanInBed == 27_000)
    }

    // MARK: - Fixtures

    @Test("A contributor asks for attention when its score or its measurement is outside his usual range the worse way")
    func unusualContributors() throws {
        let usual = try #require(MetricBaseline.of(Array(repeating: 70, count: 12) + [72, 68]))
        func oura(_ contributor: OuraContributor, _ score: Int) -> OuraContributorReading {
            OuraContributorReading(contributor: contributor, score: OuraScore(score), baseline: usual)
        }
        let lowScore = ReadinessContributorReading(contributor: .hrvBalance, oura: oura(.hrvBalance, 40), basis: nil)
        let usualScore = ReadinessContributorReading(contributor: .sleepBalance, oura: oura(.sleepBalance, 71), basis: nil)
        // Higher is better for every score, so one well above his band is not a reason to look.
        let highScore = ReadinessContributorReading(contributor: .previousNight, oura: oura(.previousNight, 95), basis: nil)
        let lowMeasurement = ReadinessContributorReading(
            contributor: .restingHeartRate, oura: nil,
            basis: .measured(MetricReading(metric: .hrv, day: Self.today, value: 40, baseline: usual, recent: [])))
        let nothingRead = ReadinessContributorReading(contributor: .bodyTemperature, oura: nil, basis: nil)

        #expect(lowScore.isUnusual)
        #expect(!usualScore.isUnusual)
        #expect(!highScore.isUnusual)
        #expect(lowMeasurement.isUnusual)
        #expect(!nothingRead.isUnusual)
    }

    private static func scored(_ date: CalendarDay, hrvBalance: Int) -> OuraDay {
        let plain = ARecovery.day(date)
        return OuraDay(
            date: date, activity: plain.activity,
            readiness: OuraDay.Readiness(
                score: OuraScore(80), temperatureDeviation: 0.1, temperatureTrendDeviation: nil,
                contributors: .init(
                    activityBalance: nil, bodyTemperature: OuraScore(90), hrvBalance: OuraScore(hrvBalance),
                    previousDayActivity: nil, previousNight: OuraScore(75), recoveryIndex: OuraScore(60),
                    restingHeartRate: OuraScore(70), sleepBalance: OuraScore(65), sleepRegularity: nil)),
            sleep: plain.sleep, spo2: nil, stress: nil, resilience: nil, vo2Max: nil, cardiovascularAge: nil)
    }

    private static func night(start: Date, hours: Double, samples: [Double?]) -> OuraEvent<OuraSleepPeriod> {
        let plain = ARecovery.night(Self.today).reading
        return OuraEvent(
            ouraId: UUID(), day: Self.today,
            period: .interval(start: start, end: start.addingTimeInterval(hours * 3600)),
            reading: OuraSleepPeriod(
                kind: plain.kind, timeInBedSeconds: plain.timeInBedSeconds, totalSleepSeconds: plain.totalSleepSeconds,
                deepSleepSeconds: 0, lightSleepSeconds: 0, remSleepSeconds: 0, awakeSeconds: 0, latencySeconds: 0,
                efficiency: 90, restlessPeriods: 0, averageHeartRate: 55, lowestHeartRate: 47, averageHrv: 60,
                averageBreath: 14, readinessScoreDelta: nil, sleepScoreDelta: nil, hypnogram: "",
                heartRate: OuraSampleSeries(intervalSeconds: 300, samples: samples, startedAt: start),
                hrv: .empty, isMainSleep: true))
    }
}
