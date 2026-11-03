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

    @Test("The log is grouped by month, newest month first, newest session first within it")
    func theLogIsReadBackwards() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-02-10")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-01")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111113", date: "2026-03-03"))]
            """)

        let log = SessionLog.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today)

        #expect(log.months.map(\.start) == [
            CalendarDay(year: 2026, month: 3, day: 1),
            CalendarDay(year: 2026, month: 2, day: 1),
        ])
        #expect(log.months[0].sessions.map(\.day) == [
            CalendarDay(year: 2026, month: 3, day: 3),
            CalendarDay(year: 2026, month: 3, day: 1),
        ])
        #expect(log.sessionCount == 3)
    }

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
        #expect(found.months.first?.sessions.first?.headline == "Barbell Back Squat")
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

    @Test("A query that matches nothing leaves no empty months behind")
    func unmatchedMonthsAreDropped() throws {
        let found = try Self.logOfBenchAndSquat().filtered(by: "zercher")

        #expect(found.months.isEmpty)
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

/// How one performed set reads, for every shape the server can send.
@Suite("Set reading")
struct SetReadingTests {
    @Test("A lift reads as reps by weight, with its RPE")
    func aLiftReadsAsRepsByWeight() throws {
        let set = try Decoding.decode(
            SetEntry.self, Session.liftSet(reps: 5, weight: 92.5, unit: 0, rpe: "8.5"))

        let reading = SetReading.of(set)

        #expect(reading.measurement == "5 × 92.5 kg")
        #expect(reading.rpe == 8.5)
    }

    @Test("An unrated set carries no RPE at all, rather than a zero")
    func anUnratedSetHasNoRpe() throws {
        // `Rpe` is two cases on purpose, and this is where that distinction finally shows: an
        // unrated set must not be drawn as RPE 0, which is a claim the athlete never made.
        let set = try Decoding.decode(SetEntry.self, Session.liftSet(rpe: "null"))

        #expect(SetReading.of(set).rpe == nil)
    }

    @Test("A whole-number weight reads without a trailing decimal")
    func wholeWeightsLoseTheDecimal() throws {
        let set = try Decoding.decode(SetEntry.self, Session.liftSet(weight: 100, unit: 0))

        #expect(SetReading.of(set).measurement == "5 × 100 kg")
    }

    @Test("A distance-and-time set reads as distance in a clock time")
    func aDistanceTimeSetReadsAsAPace() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"distanceTime","source":{"$type":"formed"},"facets":[],
             "movementDataId":"22222222-2222-2222-2222-222222222222","distance":5,
             "unit":3,"duration":"00:24:10","rpe":7}
            """)

        #expect(SetReading.of(set).measurement == "5 km in 24:10")
    }

    @Test("A trail run carries its elevation, which is the thing that makes it one")
    func aTrailRunCarriesItsGain() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"trailRun","source":{"$type":"formed"},"facets":[],"distance":12,
             "unit":3,"duration":"01:18:00","elevationGainMeters":420,"rpe":null}
            """)

        #expect(SetReading.of(set).measurement == "12 km in 1:18:00, 420 m gain")
    }

    @Test("A freeform set reads as the words it was logged with")
    func aFreeformSetKeepsItsWords() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"freeform","source":{"$type":"texted","rawText":"carried logs uphill",
              "structureConfidence":0},"facets":[]}
            """)

        #expect(SetReading.of(set).measurement == "carried logs uphill")
    }

    @Test("A set shape this build cannot read still shows the athlete's own words")
    func anUnknownSetShapeFallsBackToItsWords() throws {
        // The shape is unknown; the words are not. Losing them would lose the only record of the
        // set the athlete actually made.
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"isometric","source":{"$type":"texted","rawText":"90s plank",
              "structureConfidence":0},"facets":[]}
            """)

        #expect(SetReading.of(set).measurement == "90s plank")
    }

    @Test("A set shape this build cannot read, with no words, says so rather than showing nothing")
    func anUnknownSetShapeWithoutWordsIsNamed() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"isometric","source":{"$type":"formed"},"facets":[]}
            """)

        #expect(SetReading.of(set).measurement == "A set shape this version cannot read (isometric)")
    }
}
