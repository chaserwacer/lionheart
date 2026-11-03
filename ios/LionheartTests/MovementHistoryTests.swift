import Foundation
import Testing

@testable import Lionheart

/// A movement's history, its chart series and how often it is trained, all read from the log.
@Suite("Movement history")
struct MovementHistoryTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 3)
    private static let dip = "dddddddd-dddd-dddd-dddd-dddddddddddd"
    private static let pausedSquat = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"
    private static let highBarSquat = "ffffffff-ffff-ffff-ffff-ffffffffffff"

    private static func id(_ n: Int) -> String { String(format: "00000000-0000-0000-0000-%012d", n) }

    private static func lift(_ movement: String, _ weight: Double, _ reps: Int, unit: Int = 1) -> String {
        Session.liftSet(movementId: movement, reps: reps, weight: weight, unit: unit, rpe: "null")
    }

    private static func completed(_ n: Int, _ date: String, _ sets: [String]) -> String {
        Session.completed(
            id: id(n), date: date, sets: "[\(sets.joined(separator: ","))]",
            period: #"{"$type":"instant","at":"\#(date)T18:00:00+00:00"}"#)
    }

    private static func close(_ value: Double?, _ expected: Double) -> Bool {
        value.map { abs($0 - expected) < 0.0001 } ?? false
    }

    private static func sessions(_ json: [String]) throws -> [TrainingSession] {
        try Decoding.list(TrainingSession.self, "[\(json.joined(separator: ","))]")
    }

    // MARK: - The estimate

    @Test("Epley estimates a single from a set of twelve or fewer")
    func epleyWithinTheCap() {
        #expect(Self.close(EstimatedOneRepMax.of(weight: 300, reps: 5), 350))
        #expect(EstimatedOneRepMax.of(weight: 300, reps: 1) == 300)
        #expect(Self.close(EstimatedOneRepMax.of(weight: 100, reps: 12), 140))
    }

    @Test("A set past twelve reps, or with no load, estimates nothing")
    func noEstimateOutsideTheReliableRange() {
        #expect(EstimatedOneRepMax.of(weight: 100, reps: 13) == nil)
        #expect(EstimatedOneRepMax.of(weight: 0, reps: 18) == nil)
        #expect(EstimatedOneRepMax.of(weight: 0, reps: 5) == nil)
    }

    // MARK: - Reading the history

    @Test("A history holds only the movement's own sets, from sessions that happened, newest first")
    func theHistoryIsTheMovementsOwn() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-10-01", [Self.lift(Self.dip, 40, 6), Self.lift(Self.pausedSquat, 225, 5)]),
            Self.completed(2, "2026-10-08", [Self.lift(Self.pausedSquat, 235, 5)]),
            Self.completed(3, "2026-10-15", [Self.lift(Self.dip, 45, 5), Self.lift(Self.dip, 45, 4)]),
            Session.skipped(id: Self.id(4), date: "2026-10-20"),
        ])

        let history = MovementHistory.of(UUID(uuidString: Self.dip)!, in: log, today: Self.today)

        #expect(history.sessions.map(\.day) == [
            CalendarDay(year: 2026, month: 10, day: 15), CalendarDay(year: 2026, month: 10, day: 1),
        ])
        #expect(history.sessions.first?.sets.map(\.order) == [0, 1])
        // The dip was the first set of session 1, so its place in that session is kept.
        #expect(history.sessions.last?.sets.map(\.order) == [0])
    }

    @Test("Variants of a movement are separate histories, never merged by name")
    func variantsStayApart() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-10-01", [Self.lift(Self.pausedSquat, 225, 5)]),
            Self.completed(2, "2026-10-08", [Self.lift(Self.highBarSquat, 275, 5)]),
        ])

        let paused = MovementHistory.of(UUID(uuidString: Self.pausedSquat)!, in: log, today: Self.today)

        #expect(paused.sessions.count == 1)
    }

    // MARK: - Series

    @Test("A series is oldest first, and a session with nothing to estimate is a gap, not a zero")
    func aSeriesSkipsWhatItCannotMeasure() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-09-01", [Self.lift(Self.dip, 0, 18)]),
            Self.completed(2, "2026-10-01", [Self.lift(Self.dip, 30, 10), Self.lift(Self.dip, 30, 8)]),
            Self.completed(3, "2026-10-08", [Self.lift(Self.dip, 45, 6)]),
        ])
        let history = MovementHistory.of(UUID(uuidString: Self.dip)!, in: log, today: Self.today)

        let series = history.series(.estimatedOneRepMax, in: .pounds)

        #expect(series.map(\.day) == [
            CalendarDay(year: 2026, month: 10, day: 1), CalendarDay(year: 2026, month: 10, day: 8),
        ])
        #expect(series.count == 2)
        #expect(Self.close(series.first?.value, 40) && Self.close(series.last?.value, 54))
        #expect(series.first.map { ($0.weight, $0.reps) } ?? (0, 0) == (30, 10))
    }

    @Test("Weights never convert: a kilogram session is not on the pound series")
    func aSeriesIsOneUnit() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-10-01", [Self.lift(Self.pausedSquat, 100, 5, unit: 0)]),
            Self.completed(2, "2026-10-08", [Self.lift(Self.pausedSquat, 225, 5)]),
        ])
        let history = MovementHistory.of(UUID(uuidString: Self.pausedSquat)!, in: log, today: Self.today)

        #expect(history.series(.heaviestSet, in: .pounds).map(\.value) == [225])
        #expect(history.units == [.pounds, .kilograms])
    }

    @Test("Reps at a load chart only the loads used in more than one session, the most used first")
    func repsAtLoad() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-09-01", [Self.lift(Self.dip, 40, 5), Self.lift(Self.dip, 50, 3)]),
            Self.completed(2, "2026-09-08", [Self.lift(Self.dip, 40, 6)]),
            Self.completed(3, "2026-09-15", [Self.lift(Self.dip, 40, 8), Self.lift(Self.dip, 45, 5)]),
            Self.completed(4, "2026-09-22", [Self.lift(Self.dip, 45, 6)]),
        ])
        let history = MovementHistory.of(UUID(uuidString: Self.dip)!, in: log, today: Self.today)

        #expect(history.repeatedLoads(in: .pounds) == [40, 45])
        #expect(history.series(.repsAt(load: 40), in: .pounds).map(\.value) == [5, 6, 8])
    }

    @Test("Best set volume is one set's load times its reps")
    func bestSetVolume() throws {
        let log = try Self.sessions([
            Self.completed(1, "2026-09-01", [Self.lift(Self.pausedSquat, 200, 8), Self.lift(Self.pausedSquat, 250, 5)]),
        ])
        let history = MovementHistory.of(UUID(uuidString: Self.pausedSquat)!, in: log, today: Self.today)

        #expect(history.series(.bestSetVolume, in: .pounds).map(\.value) == [1600])
    }

    // MARK: - Frequency

    @Test("Frequency is sessions a week from the first session's week through this one, and over the last twelve weeks")
    func frequency() throws {
        // The weeks of 14 Sep through 2 Nov are eight, with two sessions in them: 2 / 8 a week.
        let log = try Self.sessions([
            Self.completed(1, "2026-09-16", [Self.lift(Self.dip, 40, 5)]),
            Self.completed(2, "2026-10-27", [Self.lift(Self.dip, 40, 6)]),
        ])
        let history = MovementHistory.of(UUID(uuidString: Self.dip)!, in: log, today: Self.today)

        let frequency = MovementFrequency.of(history, today: Self.today)

        #expect(frequency.sessionCount == 2)
        #expect(frequency.weeks.count == 8)
        #expect(frequency.perWeek == 2.0 / 8)
        #expect(frequency.recentPerWeek == 2.0 / 12)
        #expect(frequency.weeks.map(\.sessionCount).reduce(0, +) == 2)
        #expect(frequency.weeks.contains { $0.sessionCount == 0 })
    }
}

/// The log's filter by movement, and what a row says it came to.
@Suite("Session log by movement")
struct SessionLogMovementTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 3)
    private static let pausedSquat = "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"
    private static let highBarSquat = "ffffffff-ffff-ffff-ffff-ffffffffffff"

    @Test("Filtering by a movement keeps the sessions that performed it and no variant of it")
    func filterByIdentity() throws {
        let sessions = try Decoding.list(TrainingSession.self, """
            [\(Session.completed(id: "00000000-0000-0000-0000-000000000001", date: "2026-10-01",
                sets: "[\(Session.liftSet(movementId: Self.pausedSquat))]")),
             \(Session.completed(id: "00000000-0000-0000-0000-000000000002", date: "2026-10-02",
                sets: "[\(Session.liftSet(movementId: Self.highBarSquat))]"))]
            """)
        let catalogue = MovementCatalogue(try Decoding.list(MovementData.self, """
            [\(Catalogue.movement(id: Self.pausedSquat, base: "Squat", modifier: "paused")),
             \(Catalogue.movement(id: Self.highBarSquat, base: "Squat", modifier: "high bar"))]
            """))
        let log = SessionLog.reading(sessions: sessions, catalogue: catalogue, today: Self.today)

        let filtered = log.filtered(byMovements: [UUID(uuidString: Self.pausedSquat)!])

        #expect(filtered.sessionCount == 1)
        #expect(filtered.months.first?.sessions.first?.day == CalendarDay(year: 2026, month: 10, day: 1))
        // A text search for the word both share would keep both, which is why the filter is by id.
        #expect(log.filtered(by: "squat").sessionCount == 2)
    }

    @Test("A row's total is its set count and its volume in each unit lifted in")
    func aRowTotalsItsSets() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.completed(sets: """
            [\(Session.liftSet(reps: 5, weight: 200)), \(Session.liftSet(reps: 5, weight: 200)),
             \(Session.liftSet(reps: 3, weight: 100, unit: 0))]
            """))

        let row = SessionLog.row(for: session, catalogue: MovementCatalogue(), today: Self.today)

        #expect(row.totals.setCount == 3)
        #expect(row.totals.weights.map(\.volume) == [2000, 300])
        #expect(row.lines.count == 1)
    }

    @Test("The catalogue names what it holds and reports what it does not")
    func theCatalogueReportsItsGaps() throws {
        let known = UUID(uuidString: Self.pausedSquat)!
        let unknown = UUID(uuidString: Self.highBarSquat)!
        let catalogue = MovementCatalogue(try Decoding.list(MovementData.self,
            "[\(Catalogue.movement(id: Self.pausedSquat, base: "Squat"))]"))

        #expect(catalogue.missing([known, unknown]) == [unknown])
    }
}
