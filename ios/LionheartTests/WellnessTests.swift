import Foundation
import Testing

@testable import Lionheart

/// Logging a wellness state, and reading the ratings against Oura's readiness.
@Suite("Wellness")
struct WellnessTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)

    @Test("A new day's draft has nothing chosen, and is not loggable until all four are")
    func nothingIsChosenForTheAthlete() {
        var draft = WellnessDraft.opening(on: Self.today, existing: [])
        #expect(draft.entry(today: Self.today) == nil)

        draft[.energy] = 4
        draft[.mood] = 3
        draft[.motivation] = 5
        #expect(draft.entry(today: Self.today) == nil)

        draft[.stress] = 2
        #expect(draft.entry(today: Self.today) == WellnessEntry(day: Self.today, energy: 4, mood: 3, motivation: 5, stress: 2))
    }

    @Test("A day already rated opens on its ratings, since saving replaces them")
    func aRatedDayOpensOnItsRatings() throws {
        let existing = [try Decoding.decode(WellnessState.self, Wellness.reading(
            date: "2026-03-04", motivation: 2, stress: 5, mood: 1, energy: 3))]

        let draft = WellnessDraft.opening(on: Self.today, existing: existing)

        #expect(draft.motivation == 2)
        #expect(draft.stress == 5)
        #expect(draft.mood == 1)
        #expect(draft.energy == 3)
    }

    @Test("A rating off the scale is not taken, and a future day is not loggable")
    func offTheScaleAndTheFutureAreRefused() {
        var draft = WellnessDraft(day: Self.today.adding(days: 1), energy: 3, mood: 3, motivation: 3, stress: 3)
        #expect(draft.entry(today: Self.today) == nil)

        draft.day = Self.today
        draft[.energy] = 7
        #expect(draft.energy == nil)
    }

    @Test("Readiness is read for each band of overall rating, and only days with both count")
    func bandsAreMeanReadiness() throws {
        let entries = try [("2026-03-01", 1.5), ("2026-03-02", 4.0), ("2026-03-03", 4.5), ("2026-03-04", 3.0)]
            .map { try Decoding.decode(WellnessState.self, Wellness.reading(date: $0.0, overall: $0.1)) }
        let days = [
            ARecovery.day(CalendarDay(year: 2026, month: 3, day: 1), readiness: 60),
            ARecovery.day(CalendarDay(year: 2026, month: 3, day: 2), readiness: 80),
            ARecovery.day(CalendarDay(year: 2026, month: 3, day: 3), readiness: 90),
        ]

        let reading = WellnessAgainstReadiness.of(entries: entries, ouraDays: days)

        #expect(reading.paired.count == 3)
        #expect(reading.bands.map(\.label) == ["Rated under 2.5", "Rated 3.5 and up"])
        #expect(reading.bands.last?.meanReadiness == 85)
        // Three pairs are too few to state a correlation.
        #expect(reading.correlation == nil)
    }

    @Test("Correlation is Pearson's r, and undefined when either never varies")
    func correlationIsPearson() {
        let rising = (1...10).map { (Double($0), Double($0) * 2 + 1) }
        #expect(abs((WellnessAgainstReadiness.pearson(rising) ?? 0) - 1) < 1e-9)

        let falling = (1...10).map { (Double($0), 100 - Double($0)) }
        #expect(abs((WellnessAgainstReadiness.pearson(falling) ?? 0) + 1) < 1e-9)

        let flat = (1...10).map { (3.0, Double($0)) }
        #expect(WellnessAgainstReadiness.pearson(flat) == nil)
    }

    @Test("A logged entry crosses the wire as the day's midnight and its four ratings")
    func anEntryEncodes() throws {
        let entry = WellnessEntry(day: Self.today, energy: 4, mood: 3, motivation: 5, stress: 2)

        let json = try #require(JSONSerialization.jsonObject(
            with: JSONEncoder().encode(AddWellnessStateRequest(entry))) as? [String: Any])

        #expect(json["date"] as? String == "2026-03-04T00:00:00")
        #expect(json["energy"] as? Int == 4)
        #expect(json["mood"] as? Int == 3)
        #expect(json["motivation"] as? Int == 5)
        #expect(json["stress"] as? Int == 2)
    }

    @Test("A correction carries the sets whole, the period with its offset, and the notes")
    func aRevisionEncodes() throws {
        let start = Date(timeIntervalSince1970: 1_780_000_000)
        let request = ReviseSessionRequest(
            sets: [.lift(.init(movementDataId: UUID(), reps: 5, weight: 225, unit: .pounds, rpe: .unrated, kind: .warmUp))],
            period: PeriodRequest(start: start, end: start.addingTimeInterval(3600)),
            notes: "corrected")

        let json = try #require(JSONSerialization.jsonObject(with: JSONEncoder().encode(request)) as? [String: Any])

        let sets = try #require(json["sets"] as? [[String: Any]])
        #expect(sets.first?["$type"] as? String == "lift")
        #expect(sets.first?["kind"] as? Int == 1)
        let period = try #require(json["period"] as? [String: Any])
        #expect((period["start"] as? String)?.contains("T") == true)
        #expect(period["end"] != nil)
        #expect(json["notes"] as? String == "corrected")
    }
}
