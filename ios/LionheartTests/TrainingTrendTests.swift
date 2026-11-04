import Foundation
import Testing

@testable import Lionheart

/// The two readings the trends screen draws, both pure functions of the same session window.
@Suite("Training trends")
struct TrainingTrendTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)
    private static let bench = "22222222-2222-2222-2222-222222222222"
    private static let squat = "cccccccc-cccc-cccc-cccc-cccccccccccc"

    private static func catalogue() throws -> MovementCatalogue {
        MovementCatalogue(try Decoding.list(MovementData.self, """
            [\(Catalogue.movement(id: bench, base: "Bench Press")),
             \(Catalogue.movement(id: squat, base: "Back Squat"))]
            """))
    }

    // MARK: - Weeks

    @Test("A week with no training is kept as a zero, so a break reads as a break")
    func emptyWeeksAreNotSkipped() throws {
        // Skipping them is the easy thing to write and it draws unbroken training across a
        // fortnight off — the opposite of what the chart is for.
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-02-16")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-02"))]
            """)

        let weeks = TrainingTrends.weeks(of: sessions, today: Self.today)

        #expect(weeks.count == 3)
        #expect(weeks.map(\.sessionCount) == [1, 0, 1])
    }

    @Test("Weeks run oldest first, which is the direction a chart is read")
    func weeksRunForwards() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-03-02")),
             \(Session.completed(id: "11111111-1111-1111-1111-111111111112", date: "2026-02-16"))]
            """)

        let weeks = TrainingTrends.weeks(of: sessions, today: Self.today)

        #expect(weeks.map(\.start) == weeks.map(\.start).sorted())
    }

    @Test("Two sessions in one week are counted together, with their sets")
    func sessionsInAWeekAccumulate() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-03-02",
                sets: "[\(Session.liftSet(movementId: Self.bench))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03",
                sets: """
                    [\(Session.liftSet(movementId: Self.bench)),
                     \(Session.liftSet(movementId: Self.bench))]
                    """))]
            """)

        let weeks = TrainingTrends.weeks(of: sessions, today: Self.today)

        #expect(weeks.count == 1)
        #expect(weeks[0].sessionCount == 2)
        #expect(weeks[0].setCount == 3)
    }

    @Test("Only sessions that happened are charted")
    func plannedAndSkippedSessionsAreNotLoad() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "11111111-1111-1111-1111-111111111111", date: "2026-03-02")),
             \(Session.skipped(id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03")),
             \(Session.planned(id: "11111111-1111-1111-1111-111111111113", date: "2026-03-06"))]
            """)

        let trends = TrainingTrends.reading(
            sessions: sessions, catalogue: try Self.catalogue(), today: Self.today)

        #expect(trends.weeks.reduce(0) { $0 + $1.sessionCount } == 1)
    }

    @Test("No training at all is no weeks, not a row of zeroes")
    func nothingLoggedIsNoChart() throws {
        let trends = TrainingTrends.reading(
            sessions: [], catalogue: MovementCatalogue(), today: Self.today)

        #expect(trends.weeks.isEmpty)
        #expect(trends.isEmpty)
    }

    // MARK: - Strength

    @Test("A movement's series is the heaviest set of each session")
    func theSeriesTakesTheTopSetPerSession() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-03-01",
                sets: """
                    [\(Session.liftSet(movementId: Self.bench, weight: 90, unit: 0)),
                     \(Session.liftSet(movementId: Self.bench, weight: 95, unit: 0)),
                     \(Session.liftSet(movementId: Self.bench, weight: 92.5, unit: 0))]
                    """)),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 97.5, unit: 0))]"))]
            """)

        let series = TrainingTrends.strengthSeries(of: sessions, catalogue: try Self.catalogue())

        #expect(series.count == 1)
        #expect(series[0].points.map(\.weight) == [95, 97.5])
    }

    @Test("A change of unit starts its own series rather than putting a cliff in one line")
    func unitsDoNotShareASeries() throws {
        // 100 kg and 225 lb are the same lift. Charted on one axis they are a collapse and a
        // recovery that never happened.
        // Two sessions in each unit, because one point is not a series.
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-02-02",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 200, unit: 1))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-02-09",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 205, unit: 1))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111113", date: "2026-03-01",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 92.5, unit: 0))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111114", date: "2026-03-03",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 95, unit: 0))]"))]
            """)

        let series = TrainingTrends.strengthSeries(of: sessions, catalogue: try Self.catalogue())

        #expect(series.count == 2)
        #expect(Set(series.map(\.unit)) == [.kilograms, .pounds])
    }

    @Test("One session is not a trend, so a single point is no series")
    func oneSessionIsNotASeries() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                date: "2026-03-01",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 90, unit: 0))]"))]
            """)

        #expect(TrainingTrends.strengthSeries(
            of: sessions, catalogue: try Self.catalogue()).isEmpty)
    }

    @Test("The movement with the most history comes first")
    func theLongestSeriesLeads() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-03-01",
                sets: """
                    [\(Session.liftSet(movementId: Self.bench, weight: 90, unit: 0)),
                     \(Session.liftSet(movementId: Self.squat, weight: 140, unit: 0))]
                    """)),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-03-02",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 92.5, unit: 0))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111113", date: "2026-03-03",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 95, unit: 0))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111114", date: "2026-03-04",
                sets: "[\(Session.liftSet(movementId: Self.squat, weight: 145, unit: 0))]"))]
            """)

        let series = TrainingTrends.strengthSeries(of: sessions, catalogue: try Self.catalogue())

        #expect(series.map(\.movementName) == ["Bench Press (Barbell)", "Back Squat (Barbell)"])
    }

    @Test("A series' points run oldest first")
    func pointsRunForwards() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-03-03",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 95, unit: 0))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-03-01",
                sets: "[\(Session.liftSet(movementId: Self.bench, weight: 90, unit: 0))]"))]
            """)

        let series = TrainingTrends.strengthSeries(of: sessions, catalogue: try Self.catalogue())

        #expect(series[0].points.map(\.day) == [
            CalendarDay(year: 2026, month: 3, day: 1),
            CalendarDay(year: 2026, month: 3, day: 3),
        ])
    }

    @Test("A point carries the reps it was done for, so it cannot be misread")
    func aPointCarriesItsReps() throws {
        // 100 kg for one and 100 kg for five are different claims; a point without its reps
        // invites the wrong comparison.
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(
                id: "11111111-1111-1111-1111-111111111111", date: "2026-03-01",
                sets: "[\(Session.liftSet(movementId: Self.bench, reps: 3, weight: 90, unit: 0))]")),
             \(Session.completed(
                id: "11111111-1111-1111-1111-111111111112", date: "2026-03-03",
                sets: "[\(Session.liftSet(movementId: Self.bench, reps: 1, weight: 100, unit: 0))]"))]
            """)

        let series = TrainingTrends.strengthSeries(of: sessions, catalogue: try Self.catalogue())

        #expect(series[0].points.map(\.reps) == [3, 1])
    }

    @Test("Monday is the start of the week")
    func weeksStartOnMonday() {
        // 4 March 2026 is a Wednesday.
        let start = TrainingTrends.weekStart(of: Self.today)

        #expect(start == CalendarDay(year: 2026, month: 3, day: 2))
    }
}
