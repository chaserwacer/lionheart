import Foundation
import Testing

@testable import Lionheart

/// History's day and week pages: what a day and a week are read out of.
@Suite("Diary days and weeks")
struct DiaryTests {
    private static let monday = CalendarDay(year: 2026, month: 3, day: 2)

    @Test("A logged run is shown with its session; the ring's own workouts are listed apart")
    func loggedRunsAndRingWorkoutsAreApart() throws {
        let logged = UUID()
        let report = try Self.report(sessions: [
            Self.cardio(source: #"{"$type":"loggedSession","trainingSessionId":"\#(logged.uuidString)","isTrailRun":true}"#,
                        start: "2026-03-02T17:00:00Z"),
            Self.cardio(source: #"{"$type":"ringWorkout","ouraId":"99999999-9999-9999-9999-999999999999","activity":"cycling","category":"endurance","entry":"entered"}"#,
                        start: "2026-03-02T19:00:00Z"),
        ])

        let day = DiaryDay.reading(
            day: CalendarDay(Self.instant("2026-03-02T19:00:00Z")),
            ouraDays: [], nights: [], cardio: report, wellness: [])

        #expect(day.ringWorkouts.count == 1)
        #expect(day.cardio(of: logged) != nil)
        #expect(day.cardio.count == 2)
    }

    @Test("A day's recovery line is the ring's measurements of that night")
    func theRecoveryLineIsTheNight() throws {
        let day = Self.monday
        let reading = DiaryDay.reading(
            day: day,
            ouraDays: [ARecovery.day(day, temperature: 0.2)],
            nights: [ARecovery.night(day, hrv: 61), ARecovery.night(day.adding(days: -1), hrv: 40)],
            cardio: try Self.report(sessions: []),
            wellness: [])

        let line = Dictionary(uniqueKeysWithValues: reading.recovery.map { ($0.metric, $0.value) })
        #expect(line[.hrv] == 61)
        // Oura's 0.2 °C deviation, read in °F.
        #expect(abs((line[.temperatureDeviation] ?? 0) - 0.36) < 0.000_001)
        #expect(line[.sleepDuration] == 27_000)
    }

    @Test("A week's means are over the days that carry each figure, today's steps left out")
    func weekMeansSkipUnfinishedSteps() throws {
        let days = (0..<7).map { offset in
            ARecovery.day(Self.monday.adding(days: offset), readiness: 70 + offset, steps: 10_000)
        }
        let today = Self.monday.adding(days: 3)
        let week = DiaryWeek.reading(
            weekStarting: Self.monday,
            ouraDays: Array(days.prefix(4)),
            nights: [],
            cardio: try Self.report(sessions: []),
            wellness: [])

        let means = Dictionary(uniqueKeysWithValues: week.means(today: today).map { ($0.metric, $0) })
        #expect(means[.readiness]?.value == 71.5)
        #expect(means[.readiness]?.days == 4)
        // Thursday is today: its steps are still accruing.
        #expect(means[.steps]?.days == 3)
    }

    @Test("A week's lifting is its performed sessions, warm-ups left out of the sets")
    func weekLiftingCountsTheWork() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-03-02",
                sets: "[\(Session.liftSet(reps: 5, weight: 100)),\(Session.liftSet(reps: 5, weight: 200))]")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-09",
                sets: "[\(Session.liftSet())]")),
             \(Session.planned(id: "11111111-1111-1111-1111-111111111113", date: "2026-03-05"))]
            """)

        let training = WeekTraining.of(sessions, weekStarting: Self.monday, today: Self.monday.adding(days: 6))

        #expect(training.sessions.count == 1)
        #expect(training.totals.setCount == 2)
        #expect(training.totals.weights.first?.volume == 1_500)
    }

    // MARK: - Fixtures

    private static func instant(_ wire: String) -> Date {
        ISO8601DateFormatter().date(from: wire)!
    }

    private static func cardio(source: String, start: String) -> String {
        let end = ISO8601DateFormatter().string(from: instant(start).addingTimeInterval(1800))
        return """
            {"source":\(source),"start":"\(start)","end":"\(end)","distanceMeters":5000,
             "heartRate":{"$type":"notRecorded"}}
            """
    }

    private static func report(sessions: [String]) throws -> CardioReport {
        try Decoding.decode(CardioReport.self, """
            {"range":{"from":"2026-03-02","to":"2026-03-08"},"maxHeartRate":null,
             "sessions":[\(sessions.joined(separator: ","))],"weeks":[],"vo2Max":[],
             "cardiovascularAge":[],"restingHeartRate":[],
             "aerobicBase":{"recent":{"from":"2026-02-09","to":"2026-03-08"},
               "before":{"from":"2025-12-15","to":"2026-02-08"},
               "zone2MinutesPerWeek":{"recent":null,"before":null},
               "cardioMinutesPerWeek":{"recent":null,"before":null},
               "vo2Max":{"recent":null,"before":null},
               "restingHeartRate":{"recent":null,"before":null},
               "cardiovascularAge":{"recent":null,"before":null}},
             "excluded":[]}
            """)
    }
}
