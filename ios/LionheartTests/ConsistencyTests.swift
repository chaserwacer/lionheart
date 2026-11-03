import Foundation
import Testing

@testable import Lionheart

/// Consistency as measurement: runs of weeks with a session, the figures of a span, and the
/// calendar the training days are drawn on.
@Suite("Consistency")
struct ConsistencyTests {
    private static func week(_ month: Int, _ day: Int, sessions: Int) -> TrainingWeek {
        TrainingWeek(start: CalendarDay(year: 2026, month: month, day: day), sessionCount: sessions, setCount: sessions * 10)
    }

    private static let weeks = [
        week(9, 7, sessions: 3),
        week(9, 14, sessions: 2),
        week(9, 21, sessions: 0),
        week(9, 28, sessions: 1),
        week(10, 5, sessions: 4),
        week(10, 12, sessions: 3),
    ]

    @Test("Runs are maximal stretches of weeks that each held a session")
    func runs() {
        let runs = ConsistencyReading.runs(in: Self.weeks)

        #expect(runs.map(\.weekCount) == [2, 3])
        #expect(runs.last?.firstWeek == CalendarDay(year: 2026, month: 9, day: 28))
    }

    @Test("A run still counts on a Monday: an empty week in progress does not end it")
    func theCurrentRunSurvivesTheWeekInProgress() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "00000000-0000-0000-0000-000000000001", date: "2026-10-21")),
             \(Session.completed(id: "00000000-0000-0000-0000-000000000002", date: "2026-10-28"))]
            """)
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!

        // Monday 2 November: this week has nothing yet, the two before it each had one.
        let monday = ConsistencyReading.of(sessions: sessions, today: CalendarDay(year: 2026, month: 11, day: 2), calendar: calendar)
        // A week later the empty week is over, and the run has ended.
        let later = ConsistencyReading.of(sessions: sessions, today: CalendarDay(year: 2026, month: 11, day: 9), calendar: calendar)

        #expect(monday.currentRun?.weekCount == 2)
        #expect(later.currentRun == nil)
        #expect(later.longestRun?.weekCount == 2)
    }

    @Test("A span counts its sessions, its weeks trained, and the median of the weeks that had any")
    func aWindow() {
        let reading = ConsistencyReading(weeks: Self.weeks, trainingDays: [], currentRun: nil, longestRun: nil)

        let window = reading.window(lastWeeks: 4)
        // Six weeks of history: the average needs four, so the first shown week has none.
        #expect(window.average.count == 3)

        #expect(window.sessions == 8)
        #expect(window.weeksTrained == 3)
        #expect(window.perWeek == 2)
        #expect(window.medianInTrainedWeeks == 3)
    }

    @Test("A window's average reaches back past its first week, so the line starts with the bars")
    func aWindowsAverageReachesBack() {
        let window = ConsistencyReading.window(of: Self.weeks, lastWeeks: 2)

        #expect(window.weeks.map(\.sessionCount) == [4, 3])
        // Four-week means ending on each shown week: (2+0+1+4)/4 and (0+1+4+3)/4.
        #expect(window.average.map(\.value) == [1.75, 2])
    }

    @Test("A calendar page counts the days on it that held training")
    func trainedDaysOnAPage() {
        let reading = ConsistencyReading(
            weeks: [], trainingDays: [CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 10, day: 30)],
            currentRun: nil, longestRun: nil)

        #expect(reading.trainedDays(in: CalendarMonth.page(starting: CalendarDay(year: 2026, month: 11, day: 1))) == 1)
    }

    @Test("A calendar page starts its weeks on Monday and fills every row to seven")
    func aCalendarPage() {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!

        // 1 November 2026 is a Sunday.
        let page = CalendarMonth.page(starting: CalendarDay(year: 2026, month: 11, day: 1), calendar: calendar)

        #expect(page.weeks.first?.prefix(6).allSatisfy { $0 == nil } == true)
        #expect(page.weeks.first?.last == CalendarDay(year: 2026, month: 11, day: 1))
        #expect(page.weeks.allSatisfy { $0.count == 7 })
        #expect(page.weeks.joined().compactMap(\.self).count == 30)
    }

    @Test("The calendar runs from the first session's month through this one, newest first")
    func theMonths() {
        let months = CalendarMonth.months(
            from: CalendarDay(year: 2024, month: 8, day: 10), through: CalendarDay(year: 2026, month: 11, day: 3))

        #expect(months.count == 28)
        #expect(months.first?.start == CalendarDay(year: 2026, month: 11, day: 1))
        #expect(months.last?.start == CalendarDay(year: 2024, month: 8, day: 1))
    }
}

/// Which sets carry a record mark, read from the records the server established.
@Suite("Record marks")
struct RecordMarksTests {
    private static let bench = UUID(uuidString: "22222222-2222-2222-2222-222222222222")!
    private static let sessionId = UUID(uuidString: "11111111-1111-1111-1111-111111111111")!
    private static let start = Date(timeIntervalSince1970: 1_790_000_000)

    private static func lift(_ weight: Double, _ reps: Int) -> SetEntry {
        .lift(SetEntry.Lift(
            common: SetEntry.Common(source: .formed, facets: []),
            movementDataId: bench, reps: reps, weight: weight, unit: .pounds, rpe: .unrated))
    }

    private static func record(
        _ achievement: Achievement, at instant: Date = start,
        standing: PersonalRecord.Standing = .standing,
        lineage: PersonalRecord.Lineage = .replaced(previousAchievedAt: Date(timeIntervalSince1970: 0))
    ) -> PersonalRecord {
        PersonalRecord(
            personalRecordId: UUID(), subject: .movement(bench), achievement: achievement,
            achievedAt: instant, lineage: lineage, standing: standing)
    }

    private static func heaviest(_ weight: Double, _ reps: Int) -> Achievement {
        .heaviestSet(Achievement.Lift(weight: weight, unit: .pounds, reps: reps, estimatedOneRepMax: 0))
    }

    private static let session = RecordMarks.Session(
        sessionId: sessionId,
        startedAt: start,
        sets: [lift(225, 5), lift(245, 3), lift(245, 3)].enumerated().map {
            SessionDetail.IndexedSet(order: $0.offset, entry: $0.element)
        })

    private static func address(_ order: Int) -> RecordMarks.SetAddress {
        RecordMarks.SetAddress(sessionId: sessionId, order: order)
    }

    @Test("A record marks the set it holds, in the session that began at its instant")
    func aRecordMarksItsSet() {
        let marks = RecordMarks.of(
            [Self.record(Self.heaviest(245, 3)),
             Self.record(.repMax(reps: 3, Achievement.Lift(weight: 245, unit: .pounds, reps: 3, estimatedOneRepMax: 0)))],
            in: [Self.session])

        // Two identical sets: the first carries it.
        #expect(marks.kinds(of: Self.address(1)) == [.heaviestSet, .repMax(reps: 3)])
        #expect(marks.kinds(of: Self.address(2)).isEmpty)
        #expect(marks.outcome(of: Self.address(1)) == .beats(.heaviestSet, also: [.repMax(reps: 3)]))
        #expect(marks.markedSetCount(in: Self.sessionId) == 1)
    }

    @Test("A beaten record still marks the set that set it")
    func aBeatenRecordStillMarks() {
        let marks = RecordMarks.of([Self.record(Self.heaviest(245, 3), standing: .beaten)], in: [Self.session])

        #expect(marks.kinds(of: Self.address(1)) == [.heaviestSet])
    }

    @Test("A first of its kind, or a withdrawn record, marks nothing")
    func firstsAndRetractionsMarkNothing() {
        let marks = RecordMarks.of(
            [Self.record(Self.heaviest(245, 3), lineage: .firstOfItsKind),
             Self.record(Self.heaviest(225, 5), standing: .retracted)],
            in: [Self.session])

        #expect(marks.isEmpty)
    }

    @Test("A record from a session that began at another instant marks nothing here")
    func anotherSessionsRecord() {
        let marks = RecordMarks.of(
            [Self.record(Self.heaviest(245, 3), at: Self.start.addingTimeInterval(3600))], in: [Self.session])

        #expect(marks.isEmpty)
    }

    @Test("A best session volume belongs to the session, not to a set")
    func sessionVolumeIsTheSessions() {
        let marks = RecordMarks.of(
            [Self.record(.bestSessionVolume(Achievement.SessionVolume(volume: 2595, unit: .pounds)))],
            in: [Self.session])

        #expect(marks.setSessionVolume(in: Self.sessionId, on: .movement(Self.bench)))
        #expect(marks.sessionVolumeCount(in: Self.sessionId) == 1)
        #expect(marks.markedSetCount(in: Self.sessionId) == 0)
    }
}
