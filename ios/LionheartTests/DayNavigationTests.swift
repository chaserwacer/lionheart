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

/// A session's movements as log-book lines.
@Suite("Movement digest")
struct MovementDigestTests {
    private static let bench = "33333333-3333-3333-3333-333333333333"

    @Test("Straight sets at one load read as the load and every set's reps")
    func straightSets() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(reps: 6, weight: 330)), \(Session.liftSet(reps: 5, weight: 330)),
             \(Session.liftSet(reps: 4, weight: 330))]
            """)

        #expect(MovementDigest.line(of: sets) == .straight(weight: 330, unit: .pounds, reps: [6, 5, 4]))
    }

    @Test("Sets at changing loads read as the heaviest set and the count")
    func rampedSets() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(reps: 5, weight: 300)), \(Session.liftSet(reps: 3, weight: 330)),
             \(Session.liftSet(reps: 5, weight: 330))]
            """)

        #expect(MovementDigest.line(of: sets)
            == .ramped(top: .init(weight: 330, unit: .pounds, reps: 5), setCount: 3))
    }

    @Test("Loads in two units have no single heaviest set, so they are counted")
    func mixedUnitsAreCounted() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(weight: 100, unit: 0)), \(Session.liftSet(weight: 225, unit: 1))]
            """)

        #expect(MovementDigest.line(of: sets) == .counted(setCount: 2))
    }

    @Test("Movements keep the order they were performed in")
    func movementsInOrder() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet()), \(Session.liftSet(movementId: Self.bench)), \(Session.liftSet())]
            """)

        #expect(MovementDigest.of(sets, catalogue: MovementCatalogue()).map(\.order) == [0, 1, 2])
    }
}

/// The four-week training-load line.
@Suite("Weekly load")
struct WeeklyLoadTests {
    @Test("The four-week average starts on the fourth week and counts a week off as zero")
    func averageCountsEmptyWeeks() {
        let start = CalendarDay(year: 2026, month: 1, day: 5)
        let weeks = [20, 0, 20, 20, 40].enumerated().map { index, sets in
            TrainingWeek(start: start.adding(days: 7 * index), sessionCount: sets == 0 ? 0 : 2, setCount: sets)
        }

        let average = TrainingTrends.loadAverage(of: weeks)

        #expect(average.map(\.sets) == [15, 20])
        #expect(average.first?.start == start.adding(days: 21))
    }
}

/// What the injury forms will send.
@Suite("Injury drafts")
struct InjuryDraftTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 3)

    @Test("An injury needs a name and a start that is not in the future")
    func draftRefusals() {
        #expect(InjuryDraft.make(name: "   ", startedOn: Self.today, notes: "", today: Self.today)
            == .failure(.unnamed))
        #expect(InjuryDraft.make(
            name: "Hamstring", startedOn: Self.today.adding(days: 1), notes: "", today: Self.today)
            == .failure(.startsInTheFuture))
    }

    @Test("An accepted draft is trimmed")
    func draftTrims() throws {
        let draft = try InjuryDraft.make(
            name: "  Right hamstring ", startedOn: Self.today, notes: "  \n", today: Self.today).get()

        #expect(draft.name == "Right hamstring")
        #expect(draft.notes.isEmpty)
    }

    @Test("A pain level is held to the server's scale")
    func painIsClamped() {
        #expect(InjuryEventDraft(kind: .checkIn, painLevel: 14, notes: "").painLevel == 10)
        #expect(InjuryEventDraft(kind: .checkIn, painLevel: -1, notes: "").painLevel == 0)
    }

    @Test("Event kinds cross the wire as the server's integers")
    func kindsMatchTheServer() {
        // InjuryEventType is `treatment, checkin`, and `InjuryEvent.Kind` decodes the same order.
        #expect(InjuryEventDraft.Kind.treatment.wire == 0)
        #expect(InjuryEventDraft.Kind.checkIn.wire == 1)
        #expect(InjuryEvent.Kind(wire: InjuryEventDraft.Kind.checkIn.wire) == .checkIn)
    }
}
