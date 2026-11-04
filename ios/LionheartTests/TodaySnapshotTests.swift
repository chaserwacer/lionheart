import Foundation
import Testing

@testable import Lionheart

/// What the Today screen reads out of one window of sessions and one window of Oura days.
///
/// Every test pins `today` rather than reading the clock, so none of them can fail on a Tuesday.
@Suite("Today snapshot")
struct TodaySnapshotTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)

    // MARK: - Readiness

    @Test("The most recent Oura day with a reading is the one shown")
    func theLatestReadingWins() throws {
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-01", readinessScore: "61")),
             \(Oura.day(date: "2026-03-03", readinessScore: "88")),
             \(Oura.day(date: "2026-03-02", readinessScore: "74"))]
            """)

        let reading = TodaySnapshot.latestReadiness(in: days)

        #expect(reading?.day == CalendarDay(year: 2026, month: 3, day: 3))
        #expect(reading?.score == OuraScore(88))
    }

    @Test("A day with no readiness panel is not a reading")
    func aDayWithoutPanelsIsNotAReading() throws {
        let days = try Decoding.list(OuraDay.self, "[\(Oura.dayWithoutPanels(date: "2026-03-03"))]")

        #expect(TodaySnapshot.latestReadiness(in: days) == nil)
    }

    @Test("A readiness of zero is a score Oura issued, and is shown as one")
    func aZeroScoreIsRealData() throws {
        // The server no longer synthesizes a placeholder for a day it holds nothing for — such a
        // day is absent from the range entirely. So a zero that does arrive came from Oura, and
        // skipping past it would be the client hiding the athlete's own worst reading.
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-02", readinessScore: "74")),
             \(Oura.day(date: "2026-03-03", readinessScore: "0"))]
            """)

        let reading = TodaySnapshot.latestReadiness(in: days)

        #expect(reading?.day == CalendarDay(year: 2026, month: 3, day: 3))
        #expect(reading?.score == OuraScore(0))
    }

    @Test("A readiness Oura filed without a score is not a reading")
    func aScorelessReadinessIsNotAReading() throws {
        // Every number in the collection is independently nullable, so a readiness document with
        // no score is a real shape — and it is nothing to put a gauge on.
        let days = try Decoding.list(
            OuraDay.self, "[\(Oura.day(date: "2026-03-03", readinessScore: "null"))]")

        #expect(TodaySnapshot.latestReadiness(in: days) == nil)
    }

    @Test("A sleep score Oura did not send stays absent rather than being invented")
    func anAbsentSleepScoreStaysAbsent() throws {
        let days = try Decoding.list(
            OuraDay.self, "[\(Oura.day(date: "2026-03-03", sleepScore: "null"))]")

        let recovery = RecoveryReading.reading(
            for: CalendarDay(year: 2026, month: 3, day: 3), days: days, nights: [], today: Self.today)

        #expect(recovery?.readiness != nil)
        #expect(recovery?.figures.contains { $0.metric == .sleepScore } == false)
    }

    @Test("No Oura data at all is no reading, not a zero")
    func anEmptyWindowIsNoReading() {
        #expect(TodaySnapshot.latestReadiness(in: []) == nil)
    }

    @Test(
        "Readiness bands follow Oura's own thresholds",
        arguments: [(0, ReadinessReading.Band.payAttention), (69, .payAttention), (70, .good),
                    (84, .good), (85, .optimal), (100, .optimal)])
    func readinessBandsFollowOura(score: Int, expected: ReadinessReading.Band) {
        let reading = ReadinessReading(day: Self.today, score: OuraScore(score))

        #expect(reading.band == expected)
    }

    // MARK: - Last completed

    @Test("The last session is the latest completed one on or before today")
    func theLastSessionIsTheLatestCompletedOne() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "aaaaaaaa-0000-0000-0000-000000000001", date: "2026-02-26")),
             \(Session.completed(id: "aaaaaaaa-0000-0000-0000-000000000002", date: "2026-03-02")),
             \(Session.planned(id: "aaaaaaaa-0000-0000-0000-000000000003", date: "2026-03-03"))]
            """)

        let last = TodaySnapshot.lastCompleted(in: sessions, onOrBefore: Self.today)

        #expect(last?.envelope.date == CalendarDay(year: 2026, month: 3, day: 2))
    }

    @Test("A completed session dated after today is not the last session")
    func aFutureCompletedSessionIsNotTheLastOne() throws {
        // Logging after the fact is the common case, and nothing stops a mistyped date. Calling
        // tomorrow's entry "your last session" would read as a bug.
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "aaaaaaaa-0000-0000-0000-000000000001", date: "2026-03-02")),
             \(Session.completed(id: "aaaaaaaa-0000-0000-0000-000000000002", date: "2026-03-09"))]
            """)

        #expect(
            TodaySnapshot.lastCompleted(in: sessions, onOrBefore: Self.today)?.envelope.date
                == CalendarDay(year: 2026, month: 3, day: 2))
    }

    @Test("A session completed today counts as the last one")
    func todaysSessionCountsAsTheLastOne() throws {
        let sessions = try Decoding.list(
            TrainingSession.self, "[\(Session.completed(date: "2026-03-04"))]")

        #expect(TodaySnapshot.lastCompleted(in: sessions, onOrBefore: Self.today) != nil)
    }

    @Test("A skipped or active session is never the last completed one")
    func onlyCompletedSessionsCount() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.skipped(id: "aaaaaaaa-0000-0000-0000-000000000001", date: "2026-03-03")),
             \(Session.active(id: "aaaaaaaa-0000-0000-0000-000000000002", date: "2026-03-04")),
             \(Session.ofUnknownKind(id: "aaaaaaaa-0000-0000-0000-000000000003"))]
            """)

        #expect(TodaySnapshot.lastCompleted(in: sessions, onOrBefore: Self.today) == nil)
    }

    // MARK: - Next planned

    @Test("The next session is the soonest planned one from today onwards")
    func theNextSessionIsTheSoonestPlannedOne() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.planned(id: "bbbbbbbb-0000-0000-0000-000000000001", date: "2026-03-11")),
             \(Session.planned(id: "bbbbbbbb-0000-0000-0000-000000000002", date: "2026-03-06")),
             \(Session.planned(id: "bbbbbbbb-0000-0000-0000-000000000003", date: "2026-03-20"))]
            """)

        #expect(
            TodaySnapshot.nextPlanned(in: sessions, onOrAfter: Self.today)?.plannedFor
                == CalendarDay(year: 2026, month: 3, day: 6))
    }

    @Test("A session planned before today has been missed, and is History's business")
    func aPastPlannedSessionIsNotTheNextOne() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.planned(id: "bbbbbbbb-0000-0000-0000-000000000001", date: "2026-03-01")),
             \(Session.planned(id: "bbbbbbbb-0000-0000-0000-000000000002", date: "2026-03-07"))]
            """)

        #expect(
            TodaySnapshot.nextPlanned(in: sessions, onOrAfter: Self.today)?.plannedFor
                == CalendarDay(year: 2026, month: 3, day: 7))
    }

    @Test("A session planned for today is the next one")
    func todaysPlanIsTheNextOne() throws {
        let sessions = try Decoding.list(
            TrainingSession.self, "[\(Session.planned(date: "2026-03-04"))]")

        #expect(
            TodaySnapshot.nextPlanned(in: sessions, onOrAfter: Self.today)?.plannedFor == Self.today)
    }

    // MARK: - The whole reading

    @Test("One window answers all four of the screen's questions at the same moment")
    func oneWindowAnswersTheWholeScreen() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "cccccccc-0000-0000-0000-000000000001", date: "2026-03-02", sets: "[\(Session.liftSet())]")),
             \(Session.planned(id: "cccccccc-0000-0000-0000-000000000002", date: "2026-03-06")),
             \(Session.active(id: "cccccccc-0000-0000-0000-000000000003", date: "2026-03-04"))]
            """)
        let days = try Decoding.list(OuraDay.self, "[\(Oura.day(date: "2026-03-04", readinessScore: "79"))]")

        let snapshot = TodaySnapshot.reading(sessions: sessions, ouraDays: days, today: Self.today)

        #expect(snapshot.readiness?.score == OuraScore(79))
        #expect(snapshot.lastCompleted?.envelope.date == CalendarDay(year: 2026, month: 3, day: 2))
        #expect(snapshot.nextPlanned?.plannedFor == CalendarDay(year: 2026, month: 3, day: 6))
        #expect(snapshot.inProgress != nil)
        #expect(!snapshot.isEmpty)
    }

    @Test("A new athlete's Today is empty rather than broken")
    func anEmptyAccountReadsAsEmpty() {
        let snapshot = TodaySnapshot.reading(sessions: [], ouraDays: [], today: Self.today)

        #expect(snapshot.isEmpty)
        #expect(snapshot.readiness == nil)
        #expect(snapshot.lastCompleted == nil)
        #expect(snapshot.nextPlanned == nil)
        #expect(snapshot.inProgress == nil)
    }
}

/// What a session says about itself in one line.
@Suite("Session summary")
struct SessionSummaryTests {
    @Test("A session's movements are the distinct movements its sets touched")
    func movementsAreTheDistinctOnesTouched() throws {
        let sets = """
            [\(Session.liftSet(movementId: "22222222-2222-2222-2222-222222222222", reps: 5)),
             \(Session.liftSet(movementId: "22222222-2222-2222-2222-222222222222", reps: 3)),
             \(Session.liftSet(movementId: "55555555-5555-5555-5555-555555555555", reps: 8))]
            """
        guard case .completed(let session) = try Decoding.decode(
            TrainingSession.self, Session.completed(sets: sets))
        else {
            Issue.record("Expected .completed")
            return
        }

        let summary = SessionSummary.of(session)

        #expect(summary.setCount == 3)
        #expect(summary.movementCount == 2)
    }

    @Test("A set with no movement identity counts as no movement, not a nameless one")
    func setsWithoutAMovementAreNotCountedAsOne() throws {
        let sets = """
            [{"$type":"trailRun","source":{"$type":"formed"},"facets":[],"distance":12.4,
              "unit":2,"duration":"01:38:00","elevationGainMeters":420,"rpe":7},
             {"$type":"freeform","source":{"$type":"texted","rawText":"mobility","structureConfidence":0.1},
              "facets":[]}]
            """
        guard case .completed(let session) = try Decoding.decode(
            TrainingSession.self, Session.completed(sets: sets))
        else {
            Issue.record("Expected .completed")
            return
        }

        let summary = SessionSummary.of(session)

        #expect(summary.setCount == 2)
        #expect(summary.movementCount == 0)
    }

    @Test("An interval session has a duration; an instant session has none")
    func onlyAnIntervalHasADuration() throws {
        let interval = Session.completed(
            period: #"{"$type":"interval","start":"2026-03-04T17:00:00+00:00","end":"2026-03-04T18:00:00+00:00"}"#)
        let instant = Session.completed(
            period: #"{"$type":"instant","at":"2026-03-04T17:00:00+00:00"}"#)

        guard case .completed(let withInterval) = try Decoding.decode(TrainingSession.self, interval),
              case .completed(let withInstant) = try Decoding.decode(TrainingSession.self, instant)
        else {
            Issue.record("Expected both to be .completed")
            return
        }

        #expect(SessionSummary.of(withInterval).duration == 3600)
        #expect(SessionSummary.of(withInstant).duration == nil)
    }

    @Test("Effort counts as reported only when at least one rating was given")
    func blankRatingsDoNotCountAsReportedEffort() throws {
        let blank = Session.completed(
            effort: #"{"$type":"reported","ratings":{"recordedAt":"2026-03-04T18:15:00","accumulatedFatigue":null,"difficultyRating":null,"engagementRating":null,"externalVariablesRating":null}}"#)

        guard case .completed(let session) = try Decoding.decode(TrainingSession.self, blank) else {
            Issue.record("Expected .completed")
            return
        }

        #expect(session.effort.ratings?.isEmpty == true)
        #expect(SessionSummary.of(session).hasEffort == false)
    }

    @Test("A plan's movements are the distinct ones it prescribes")
    func aPlanCountsItsMovements() throws {
        let prescriptions = """
            [{"$type":"lift","movementDataId":"22222222-2222-2222-2222-222222222222","targetReps":5,
              "targetWeight":245,"unit":1,"targetRpe":8},
             {"$type":"lift","movementDataId":"22222222-2222-2222-2222-222222222222","targetReps":3,
              "targetWeight":265,"unit":1,"targetRpe":9},
             {"$type":"freeform","text":"finisher"}]
            """
        guard case .planned(let session) = try Decoding.decode(
            TrainingSession.self, Session.planned(prescriptions: prescriptions))
        else {
            Issue.record("Expected .planned")
            return
        }

        let summary = PlanSummary.of(session)

        #expect(summary.prescriptionCount == 3)
        #expect(summary.movementCount == 1)
    }

    @Test("Whitespace-only notes are no notes")
    func whitespaceIsNotANote() throws {
        guard case .completed(let session) = try Decoding.decode(
            TrainingSession.self, Session.completed(notes: "   "))
        else {
            Issue.record("Expected .completed")
            return
        }

        #expect(SessionSummary.of(session).hasNotes == false)
    }
}
