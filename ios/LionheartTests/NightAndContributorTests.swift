import Foundation
import Testing

@testable import Lionheart

/// The night's hypnogram, the score components against the athlete's range, and the trend lines'
/// breaks across gaps.
@Suite("Night and contributors")
struct NightAndContributorTests {
    private static let day = CalendarDay(year: 2026, month: 8, day: 1)

    // MARK: - Hypnogram

    @Test("Oura's digits decode as it documents them: 1 deep, 2 light, 3 REM, 4 awake")
    func digitsDecode() {
        #expect(SleepStage(digit: "1") == .deep)
        #expect(SleepStage(digit: "2") == .light)
        #expect(SleepStage(digit: "3") == .rem)
        #expect(SleepStage(digit: "4") == .awake)
        #expect(SleepStage(digit: "0") == nil)
    }

    @Test("Consecutive slots of a stage are one run, placed five minutes a slot from bedtime")
    func runsFromBedtime() {
        let bedtime = Date(timeIntervalSince1970: 1_000_000)
        let night = Self.night(hypnogram: "44111123")

        let runs = Hypnogram.runs(of: night, from: bedtime)

        #expect(runs.map(\.stage) == [.awake, .deep, .light, .rem])
        #expect(runs[1].start == bedtime.addingTimeInterval(600))
        #expect(runs[1].end == bedtime.addingTimeInterval(1_800))
    }

    @Test("A digit outside the documented four leaves a gap rather than a guessed stage")
    func unknownDigitIsAGap() {
        let bedtime = Date(timeIntervalSince1970: 0)

        let runs = Hypnogram.runs(of: Self.night(hypnogram: "1912"), from: bedtime)

        #expect(runs.map(\.stage) == [.deep, .deep, .light])
        #expect(runs[1].start == bedtime.addingTimeInterval(600))
    }

    @Test("Stage shares are Oura's totals over the time in bed")
    func stageShares() {
        let shares = StageShare.of(Self.night(hypnogram: ""))

        #expect(shares.map(\.stage) == [.awake, .rem, .light, .deep])
        #expect(shares.map(\.fraction).reduce(0, +) == 1)
        #expect(shares.first { $0.stage == .deep }?.seconds == 3_600)
    }

    // MARK: - Contributors

    @Test("A component well under the four weeks before it is marked, and its usual band is clipped to 0–100")
    func contributorAgainstItsRange() throws {
        let earlier = (1...20).map { offset in
            Self.readinessDay(Self.day.adding(days: -offset), hrvBalance: offset.isMultiple(of: 2) ? 96 : 100)
        }
        let today = Self.readinessDay(Self.day, hrvBalance: 70)

        let readings = ContributorReading.readings(of: .readiness, on: today, among: earlier + [today])
        let hrv = try #require(readings.first { $0.contributor == .hrvBalance })

        #expect(hrv.standing == .unfavourable)
        #expect(hrv.usualRange?.upperBound == 100)
        #expect(readings.allSatisfy { $0.contributor.group == .readiness })
    }

    @Test("A component Oura sent nothing for is left out rather than drawn at zero")
    func absentComponentIsLeftOut() {
        let today = Self.readinessDay(Self.day, hrvBalance: nil)

        let readings = ContributorReading.readings(of: .readiness, on: today, among: [today])

        #expect(!readings.contains { $0.contributor == .hrvBalance })
        #expect(readings.allSatisfy { $0.baseline == nil })
    }

    @Test("Every component belongs to exactly one score")
    func everyComponentHasAGroup() {
        let grouped = ContributorGroup.allCases.flatMap(\.contributors)

        #expect(Set(grouped) == Set(Contributor.allCases))
        #expect(grouped.count == Contributor.allCases.count)
    }

    // MARK: - Trend lines

    @Test("A mean line breaks across a gap longer than a week instead of drawing a trend through it")
    func runsBreakAtLongGaps() {
        let points = [0, 1, 2, 20, 21].map { MetricPoint(day: Self.day.adding(days: $0), value: 1) }

        let runs = MetricTrend.unbrokenRuns(of: points)

        #expect(runs.map(\.count) == [3, 2])
    }

    // MARK: - Builders

    private static func night(hypnogram: String) -> OuraSleepPeriod {
        OuraSleepPeriod(
            kind: "long_sleep", timeInBedSeconds: 30_000, totalSleepSeconds: 27_000, deepSleepSeconds: 3_600,
            lightSleepSeconds: 16_200, remSleepSeconds: 7_200, awakeSeconds: 3_000, latencySeconds: 600,
            efficiency: 90, restlessPeriods: 0, averageHeartRate: 50, lowestHeartRate: 44, averageHrv: 90,
            averageBreath: 14, readinessScoreDelta: nil, sleepScoreDelta: nil, hypnogram: hypnogram,
            heartRate: .empty, hrv: .empty, isMainSleep: true)
    }

    private static func readinessDay(_ date: CalendarDay, hrvBalance: Int?) -> OuraDay {
        OuraDay(
            date: date,
            activity: nil,
            readiness: OuraDay.Readiness(
                score: 80, temperatureDeviation: nil, temperatureTrendDeviation: nil,
                contributors: .init(
                    activityBalance: 80, bodyTemperature: 90, hrvBalance: hrvBalance, previousDayActivity: 80,
                    previousNight: 80, recoveryIndex: 80, restingHeartRate: 80, sleepBalance: 80,
                    sleepRegularity: 80)),
            sleep: nil, spo2: nil, stress: nil, resilience: nil, vo2Max: nil, cardiovascularAge: nil)
    }
}
