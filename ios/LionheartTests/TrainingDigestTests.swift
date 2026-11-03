import Foundation
import Testing

@testable import Lionheart

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

    @Test("Work that names no movement is counted on a line of its own, never dropped")
    func unattachedWorkIsCounted() throws {
        let sets = try Decoding.list(SetEntry.self, """
            [\(Session.liftSet()),
             {"$type":"freeform","source":{"$type":"texted","rawText":"carried logs"},"facets":[]}]
            """)

        let digests = MovementDigest.of(sets, catalogue: MovementCatalogue())

        #expect(digests.count == 2)
        #expect(digests.last?.line == .counted(setCount: 1))
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

    @Test("A span's average reaches back before its first bar, so the line starts where the bars do")
    func spanAverageStartsWithTheBars() {
        let start = CalendarDay(year: 2026, month: 1, day: 5)
        let weeks = [20, 0, 20, 20, 40, 10].enumerated().map { index, sets in
            TrainingWeek(start: start.adding(days: 7 * index), sessionCount: sets == 0 ? 0 : 2, setCount: sets)
        }

        let load = TrainingTrends(weeks: weeks, strength: []).load(lastWeeks: 2)

        #expect(load.weeks.map(\.setCount) == [40, 10])
        #expect(load.average.map(\.start) == load.weeks.map(\.start))
        #expect(load.sets == 50)
        #expect(load.weeksTrained == 2)
    }

    @Test("A top-set summary over a span is the latest, the best, and the change from the first")
    func strengthSummary() {
        let start = CalendarDay(year: 2026, month: 1, day: 5)
        let series = StrengthSeries(
            movementDataId: UUID(), movementName: "Squat", unit: .pounds,
            points: [(300, 5), (320, 3), (340, 1), (330, 5)].enumerated().map { index, set in
                .init(day: start.adding(days: 7 * index), weight: set.0, reps: set.1)
            })

        let summary = series.summary(since: start.adding(days: 7))

        #expect(summary?.points.count == 3)
        #expect(summary?.best.weight == 340)
        #expect(summary?.latest.weight == 330)
        #expect(summary?.change == 10)
        #expect(series.summary(since: start.adding(days: 21)) == nil)
    }
}
