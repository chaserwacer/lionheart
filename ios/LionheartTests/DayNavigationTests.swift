import Foundation
import Testing

@testable import Lionheart

/// Stepping the Today tab through days, and what a day other than today is read for.
@Suite("Day navigation")
struct DayNavigationTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)

    // MARK: - Choosing the day

    @Test("Stepping back from today lands on yesterday, and forward again lands on today itself")
    func steppingRoundTrips() {
        let yesterday = DaySelection.today.stepped(by: -1, today: Self.today)

        #expect(yesterday == .past(CalendarDay(year: 2026, month: 3, day: 3)))
        #expect(yesterday.stepped(by: 1, today: Self.today) == .today)
    }

    @Test("Nothing steps or chooses past today")
    func theFutureIsClosed() {
        #expect(DaySelection.today.stepped(by: 1, today: Self.today) == .today)
        #expect(DaySelection.of(Self.today.adding(days: 5), today: Self.today) == .today)
    }

    @Test("Today follows the clock: the same selection means the next day after midnight")
    func todayFollowsTheClock() {
        let tomorrow = Self.today.adding(days: 1)

        #expect(DaySelection.today.day(today: tomorrow) == tomorrow)
        #expect(DaySelection.past(Self.today).day(today: tomorrow) == Self.today)
    }

    @Test("Stepping across a month end moves one calendar day")
    func steppingCrossesMonths() {
        let first = CalendarDay(year: 2026, month: 3, day: 1)

        #expect(DaySelection.past(first).stepped(by: -1, today: Self.today)
            == .past(CalendarDay(year: 2026, month: 2, day: 28)))
    }

    // MARK: - Reading a past day

    @Test("A past day shows what was trained on it, and none of today's questions")
    func aPastDayIsReadForWhatHappened() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "cccccccc-0000-0000-0000-000000000001", date: "2026-03-02")),
             \(Session.completed(id: "cccccccc-0000-0000-0000-000000000002", date: "2026-03-01")),
             \(Session.planned(id: "cccccccc-0000-0000-0000-000000000003", date: "2026-03-06")),
             \(Session.active(id: "cccccccc-0000-0000-0000-000000000004", date: "2026-03-04"))]
            """)
        let day = CalendarDay(year: 2026, month: 3, day: 2)

        let snapshot = TodaySnapshot.reading(
            sessions: sessions, ouraDays: [], today: Self.today, day: day)

        #expect(snapshot.day == day)
        #expect(snapshot.sessionsOnDay.map(\.date) == [day])
        #expect(snapshot.lastCompleted == nil)
        #expect(snapshot.nextPlanned == nil)
        #expect(snapshot.inProgress == nil)
    }

    @Test("A past day's recovery is that day's, never a nearer one")
    func aPastDayReadsItsOwnRecovery() {
        let day = CalendarDay(year: 2026, month: 3, day: 2)

        let snapshot = TodaySnapshot.reading(
            sessions: [], ouraDays: [ARecovery.day(day.adding(days: -1))], today: Self.today, day: day)

        #expect(snapshot.recovery == nil)
    }

    @Test("Today with nothing logged today shows the last session; with a session today, not again")
    func theLastSessionStandsInOnlyForAnEmptyToday() throws {
        let earlier = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "cccccccc-0000-0000-0000-000000000001", date: "2026-03-02"))]
            """)
        let withToday = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "cccccccc-0000-0000-0000-000000000001", date: "2026-03-02")),
             \(Session.completed(id: "cccccccc-0000-0000-0000-000000000002", date: "2026-03-04"))]
            """)

        #expect(TodaySnapshot.reading(sessions: earlier, ouraDays: [], today: Self.today).lastCompleted != nil)

        let busy = TodaySnapshot.reading(sessions: withToday, ouraDays: [], today: Self.today)
        #expect(busy.lastCompleted == nil)
        #expect(busy.sessionsOnDay.count == 1)
    }

    @Test("Before the ring syncs, today reads the newest day within a fortnight and no further")
    func todayFallsBackAFortnightAtMost() {
        let threeDaysAgo = Self.today.adding(days: -3)
        let monthAgo = Self.today.adding(days: -20)

        let recent = TodaySnapshot.reading(
            sessions: [], ouraDays: [ARecovery.day(threeDaysAgo)], today: Self.today)
        let stale = TodaySnapshot.reading(
            sessions: [], ouraDays: [ARecovery.day(monthAgo)], today: Self.today)

        #expect(recent.recovery?.day == threeDaysAgo)
        #expect(stale.recovery == nil)
    }
}
