import Foundation
import Testing

@testable import Lionheart

/// What the History log reads out of one session window.
///
/// Every test pins `today`, so none of them can fail on a Tuesday — which matters most for the
/// "missed" reading, whose whole definition is relative to the athlete's day.
@Suite("Session log")
struct SessionLogTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)
    private static let bench = "22222222-2222-2222-2222-222222222222"
    private static let squat = "cccccccc-cccc-cccc-cccc-cccccccccccc"

    private static func catalogue() throws -> MovementCatalogue {
        MovementCatalogue(try Decoding.list(MovementData.self, """
            [\(Catalogue.movement(id: bench, base: "Bench Press")),
             \(Catalogue.movement(id: squat, base: "Back Squat"))]
            """))
    }

    // MARK: - The lifecycle read against today

    @Test("A planned session whose day has gone by has been missed, not scheduled")
    func aPastPlannedSessionIsMissed() throws {
        let session = try Decoding.decode(
            TrainingSession.self, Session.planned(date: "2026-03-01"))

        #expect(SessionLog.kind(of: session, today: Self.today) == .missed)
    }

    @Test("A session planned for today is still planned")
    func todaysPlannedSessionIsNotMissedYet() throws {
        let session = try Decoding.decode(
            TrainingSession.self, Session.planned(date: "2026-03-04"))

        #expect(SessionLog.kind(of: session, today: Self.today) == .planned)
    }

    @Test("A skipped session reads as skipped, never as completed")
    func aSkippedSessionKeepsItsOwnWord() throws {
        // The skip is data the athlete chose to record. Reading it as a completed session would
        // put training in the log that never happened.
        let session = try Decoding.decode(TrainingSession.self, Session.skipped())

        let kind = SessionLog.kind(of: session, today: Self.today)

        #expect(kind == .skipped)
        #expect(!kind.isPerformed)
    }

    @Test("A lifecycle state this build has not heard of still gets a row")
    func anUnknownStateStillAppears() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.ofUnknownKind())

        let row = SessionLog.row(
            for: session, catalogue: try Self.catalogue(), today: Self.today)

        #expect(row.kind == .unrecognised("abandoned"))
        #expect(row.sessionId.uuidString.lowercased() == Session.id)
    }

    @Test("Only sessions that actually happened count as performed")
    func onlyRealTrainingIsPerformed() {
        #expect(LoggedSessionKind.completed.isPerformed)
        #expect(LoggedSessionKind.inProgress.isPerformed)
        #expect(!LoggedSessionKind.skipped.isPerformed)
        #expect(!LoggedSessionKind.missed.isPerformed)
        #expect(!LoggedSessionKind.planned.isPerformed)
        #expect(!LoggedSessionKind.unrecognised("abandoned").isPerformed)
    }

    // MARK: - Grouping

    @Test("The log is grouped by Monday-to-Sunday week, newest first, and by day within it")
    func theLogIsReadBackwardsByWeek() throws {
        // 2026-03-03 is a Tuesday and 03-01 the Sunday before it, so they fall in different weeks.
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-02-10")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-01")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111113", date: "2026-03-03")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111114", date: "2026-03-02"))]
            """)

        let log = SessionLog.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today, calendar: Self.calendar)

        #expect(log.weeks.map(\.start) == [
            CalendarDay(year: 2026, month: 3, day: 2),
            CalendarDay(year: 2026, month: 2, day: 23),
            CalendarDay(year: 2026, month: 2, day: 9),
        ])
        #expect(log.weeks[0].days.map(\.day) == [
            CalendarDay(year: 2026, month: 3, day: 3),
            CalendarDay(year: 2026, month: 3, day: 2),
        ])
        #expect(log.weeks[0].performedCount == 2)
        #expect(log.sessionCount == 4)
    }

    @Test("Two sessions on one day are one row, in the order they happened")
    func twoSessionsOnADayAreOneDay() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-03-03")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03"))]
            """)

        let log = SessionLog.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today, calendar: Self.calendar)

        let day = try #require(log.weeks.first?.days.first)
        #expect(log.weeks.first?.days.count == 1)
        #expect(day.sessions.map(\.sessionId.uuidString) == [
            "11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111112",
        ])
    }

    @Test("A run of weeks with nothing logged is one stretch between the weeks either side")
    func quietWeeksAreSaidOnce() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-02-03")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111113", date: "2026-02-24"))]
            """)

        let stretches = SessionLog.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today, calendar: Self.calendar
        ).stretches(calendar: Self.calendar)

        // Weeks of 2 Mar and 23 Feb are back to back; between 23 Feb and 2 Feb lie the weeks of
        // 9 and 16 Feb.
        #expect(stretches.map(\.id) == [
            "week-2026-03-02", "week-2026-02-23", "quiet-2026-02-09", "week-2026-02-02",
        ])
        if case .quiet(_, let count) = stretches[2] { #expect(count == 2) } else { Issue.record("not quiet") }
    }

    /// Gregorian with the device's zone, so the week arithmetic does not depend on the runner's
    /// locale.
    private static let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .current
        return calendar
    }()

    // MARK: - Naming

    @Test("A row is headlined by the movements it touched, in the order they were touched")
    func movementsAreNamedInTheOrderTheyHappened() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.completed(
            sets: """
                [\(Session.liftSet(movementId: Self.squat)),
                 \(Session.liftSet(movementId: Self.bench))]
                """))

        let row = SessionLog.row(
            for: session, catalogue: try Self.catalogue(), today: Self.today)

        // Squat first because that is what was done first, not alphabetically.
        #expect(row.headline == "Barbell Back Squat, Barbell Bench Press")
    }

    @Test("A movement returned to later is named once, not twice")
    func aRepeatedMovementIsNamedOnce() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.completed(
            sets: """
                [\(Session.liftSet(movementId: Self.bench)),
                 \(Session.liftSet(movementId: Self.squat)),
                 \(Session.liftSet(movementId: Self.bench))]
                """))

        let row = SessionLog.row(
            for: session, catalogue: try Self.catalogue(), today: Self.today)

        #expect(row.headline == "Barbell Bench Press, Barbell Back Squat")
    }

    @Test("A session with no movements is headlined by its lifecycle word")
    func aSessionWithoutMovementsFallsBackToItsState() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.skipped())

        let row = SessionLog.row(
            for: session, catalogue: try Self.catalogue(), today: Self.today)

        #expect(row.headline == "Skipped")
    }

    @Test("A planned session is headlined by what it prescribes")
    func aPlannedSessionIsNamedByItsPrescriptions() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.planned(
            date: "2026-03-06",
            prescriptions: """
                [{"$type":"lift","movementDataId":"\(Self.bench)","targetReps":5,
                  "targetWeight":100,"unit":0,"targetRpe":8}]
                """))

        let row = SessionLog.row(
            for: session, catalogue: try Self.catalogue(), today: Self.today)

        #expect(row.headline == "Barbell Bench Press")
        #expect(row.facts == .prescribed(count: 1))
    }

    // MARK: - Search

    @Test("Search matches a movement name")
    func searchFindsAMovement() throws {
        let log = try Self.logOfBenchAndSquat()

        let found = log.filtered(by: "squat")

        #expect(found.sessionCount == 1)
        #expect(found.weeks.first?.sessions.first?.headline == "Barbell Back Squat")
    }

    @Test("Search matches a note, which is where the words a lifter remembers live")
    func searchFindsANote() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(date: "2026-03-01", notes: "hook grip held"))]
            """)
        let log = SessionLog.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today)

        #expect(log.filtered(by: "hook").sessionCount == 1)
    }

    @Test("Search ignores case")
    func searchIgnoresCase() throws {
        #expect(try Self.logOfBenchAndSquat().filtered(by: "BENCH").sessionCount == 1)
    }

    @Test("An empty query is the whole log, not an empty one")
    func anEmptyQueryFiltersNothing() throws {
        let log = try Self.logOfBenchAndSquat()

        #expect(log.filtered(by: "   ").sessionCount == log.sessionCount)
    }

    @Test("A query that matches nothing leaves no empty weeks behind")
    func unmatchedWeeksAreDropped() throws {
        let found = try Self.logOfBenchAndSquat().filtered(by: "zercher")

        #expect(found.weeks.isEmpty)
        #expect(found.isEmpty)
    }

    private static func logOfBenchAndSquat() throws -> SessionLog {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111",
                date: "2026-03-01",
                sets: "[\(Session.liftSet(movementId: bench))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112",
                date: "2026-03-02",
                sets: "[\(Session.liftSet(movementId: squat))]"))]
            """)
        return SessionLog.reading(sessions: sessions, catalogue: try catalogue(), today: today)
    }
}

/// How a session's flat set list becomes the movement blocks the detail screen shows.
@Suite("Session detail")
struct SessionDetailTests {
    private static let bench = "22222222-2222-2222-2222-222222222222"
    private static let squat = "cccccccc-cccc-cccc-cccc-cccccccccccc"

    private static func catalogue() throws -> MovementCatalogue {
        MovementCatalogue(try Decoding.list(MovementData.self, """
            [\(Catalogue.movement(id: bench, base: "Bench Press")),
             \(Catalogue.movement(id: squat, base: "Back Squat"))]
            """))
    }

    @Test("Consecutive sets on one movement are one block")
    func consecutiveSetsGroup() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(movementId: Self.bench, weight: 90)),
             \(Session.liftSet(movementId: Self.bench, weight: 92.5))]
            """)

        let detail = SessionDetail.of(sets, catalogue: try Self.catalogue())

        #expect(detail.blocks.count == 1)
        #expect(detail.blocks[0].movementName == "Barbell Bench Press")
        #expect(detail.blocks[0].sets.count == 2)
    }

    @Test("A movement returned to after another is two blocks, not one")
    func returningToAMovementStartsANewBlock() throws {
        // Collapsing these would rewrite the athlete's own order: benching, squatting, then
        // benching again is three things that happened, in that order.
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(movementId: Self.bench, weight: 90)),
             \(Session.liftSet(movementId: Self.squat, weight: 140)),
             \(Session.liftSet(movementId: Self.bench, weight: 95))]
            """)

        let detail = SessionDetail.of(sets, catalogue: try Self.catalogue())

        #expect(detail.blocks.map(\.movementName)
            == ["Barbell Bench Press", "Barbell Back Squat", "Barbell Bench Press"])
        #expect(detail.blocks.map(\.order) == [0, 1, 2])
    }

    @Test("A set's number is its position in the session, which survives the regrouping")
    func setOrderIsTheSessionsOrder() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet(movementId: Self.bench)),
             \(Session.liftSet(movementId: Self.squat)),
             \(Session.liftSet(movementId: Self.bench))]
            """)

        let detail = SessionDetail.of(sets, catalogue: try Self.catalogue())

        #expect(detail.blocks[2].sets.map(\.order) == [2])
    }

    @Test("Sets with no movement identity get their own list rather than a nameless block")
    func movementlessSetsAreUnattached() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [{"$type":"freeform","source":{"$type":"texted","rawText":"carried logs",
              "structureConfidence":0},"facets":[]},
             \(Session.liftSet(movementId: Self.bench))]
            """)

        let detail = SessionDetail.of(sets, catalogue: try Self.catalogue())

        #expect(detail.unattached.count == 1)
        #expect(detail.blocks.count == 1)
    }

    @Test("A movement the catalogue cannot resolve is said to be unknown, not hidden")
    func anUnresolvedMovementStillShowsItsSets() throws {
        let sets = try Decoding.list(
            SetEntry.self, "[\(Session.liftSet(movementId: "dddddddd-dddd-dddd-dddd-dddddddddddd"))]")

        let detail = SessionDetail.of(sets, catalogue: MovementCatalogue())

        #expect(detail.blocks.count == 1)
        #expect(detail.blocks[0].movementName == "Unknown movement")
    }

    @Test("An empty set list is an empty detail, not one empty block")
    func noSetsMeansNoBlocks() {
        #expect(SessionDetail.of([], catalogue: MovementCatalogue()).isEmpty)
    }
}
