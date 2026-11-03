import Foundation
import Testing

@testable import Lionheart

/// What the Body tab reads out of three windows.
@Suite("Body snapshot")
struct BodySnapshotTests {
    @Test("Oura days and wellness entries run newest first, the order they are read in")
    func recordsAreNewestFirst() throws {
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-01", readinessScore: 70)),
             \(Oura.day(date: "2026-03-03", readinessScore: 80)),
             \(Oura.day(date: "2026-03-02", readinessScore: 75))]
            """)
        let wellness = try Decoding.list(WellnessState.self, """
            [\(Wellness.reading(date: "2026-03-01")),
             \(Wellness.reading(date: "2026-03-03"))]
            """)

        let snapshot = BodySnapshot.reading(ouraDays: days, wellness: wellness, injuries: [])

        #expect(snapshot.ouraDays.map(\.date) == [
            CalendarDay(year: 2026, month: 3, day: 3),
            CalendarDay(year: 2026, month: 3, day: 2),
            CalendarDay(year: 2026, month: 3, day: 1),
        ])
        #expect(snapshot.wellness.first?.date == CalendarDay(year: 2026, month: 3, day: 3))
    }

    @Test("Active injuries come before resolved ones, each newest first")
    func activeInjuriesLead() throws {
        let injuries = try Decoding.list(Injury.self, """
            [\(Tracked.injury(
                id: "77777777-7777-7777-7777-777777777771", name: "Old ankle",
                injuryDate: "2026-02-01", isActive: false)),
             \(Tracked.injury(
                id: "77777777-7777-7777-7777-777777777772", name: "Shoulder",
                injuryDate: "2026-01-20", isActive: true))]
            """)

        let snapshot = BodySnapshot.reading(ouraDays: [], wellness: [], injuries: injuries)

        #expect(snapshot.injuries.map(\.name) == ["Shoulder", "Old ankle"])
        #expect(snapshot.activeInjuries.map(\.name) == ["Shoulder"])
        #expect(snapshot.resolvedInjuries.map(\.name) == ["Old ankle"])
    }

    @Test("The latest readiness follows the same rule Today uses, zeroes dropped")
    func readinessReusesTodaysRule() throws {
        // The per-date Oura route synthesizes a zero-score placeholder for a day with no data. Two
        // screens disagreeing about whether that means "not recovered" would be two different
        // claims about the athlete's recovery.
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-03", readinessScore: 0)),
             \(Oura.day(date: "2026-03-01", readinessScore: 72))]
            """)

        let snapshot = BodySnapshot.reading(ouraDays: days, wellness: [], injuries: [])

        #expect(snapshot.latestReadiness?.score == 72)
        #expect(snapshot.latestReadiness?.day == CalendarDay(year: 2026, month: 3, day: 1))
    }

    @Test("Nothing recorded at all is an empty snapshot")
    func nothingRecordedIsEmpty() {
        #expect(BodySnapshot.reading(ouraDays: [], wellness: [], injuries: []).isEmpty)
    }
}

/// How an Oura day's panels are read, which is the shape that has to absorb a widened Oura surface
/// without a rewrite.
@Suite("Oura panels")
struct OuraPanelTests {
    @Test("A day carries only the panels the server sent")
    func onlyPresentPanelsAreListed() throws {
        // The panels arrive independently: a day with sleep and no readiness is a real row, and
        // inventing a zeroed readiness for it would be the lie the range route exists to avoid.
        let day = try Decoding.decode(OuraDay.self, Oura.dayWithSleepOnly(date: "2026-03-03"))

        let panels = OuraPanel.panels(of: day)

        #expect(panels.map(\.title) == ["Sleep"])
    }

    @Test("A day with no panels at all lists none, rather than four empty ones")
    func aDayWithoutPanelsListsNothing() throws {
        let day = try Decoding.decode(OuraDay.self, Oura.dayWithoutPanels(date: "2026-03-03"))

        #expect(OuraPanel.panels(of: day).isEmpty)
    }

    @Test("Panels read in the order the screen asks about them")
    func panelsAreOrderedByWhatIsAsked() throws {
        let day = try Decoding.decode(OuraDay.self, Oura.day(date: "2026-03-03", readinessScore: 80))

        #expect(OuraPanel.panels(of: day).map(\.title)
            == ["Readiness", "Sleep", "Activity", "Resilience"])
    }

    @Test("Resilience has no score, which is a different thing from a score of zero")
    func resilienceHasNoScore() throws {
        let day = try Decoding.decode(OuraDay.self, Oura.day(date: "2026-03-03", readinessScore: 80))
        let panels = OuraPanel.panels(of: day)

        #expect(panels.first { $0.title == "Readiness" }?.score == 80)
        #expect(panels.first { $0.title == "Resilience" }?.score == nil)
    }

    @Test("Every panel says what it shows besides its score")
    func everyPanelHasFigures() throws {
        // Exhaustive over the panels, so a new one has to decide what it shows rather than
        // rendering as a bare score.
        let day = try Decoding.decode(OuraDay.self, Oura.day(date: "2026-03-03", readinessScore: 80))

        for panel in OuraPanel.panels(of: day) {
            #expect(!panel.figures.isEmpty)
        }
    }
}

/// Self-reported wellness, and the one number the client must not recompute.
@Suite("Wellness reading")
struct WellnessReadingTests {
    @Test("The overall score is the server's, printed as received")
    func overallComesFromTheServer() throws {
        // The server averages the four with stress inverted. Averaging them here would give the
        // two clients and the analyses two different numbers for one reading.
        let state = try Decoding.decode(WellnessState.self, Wellness.reading(
            date: "2026-03-03", motivation: 5, stress: 1, mood: 5, energy: 5, overall: 5))

        #expect(WellnessReading.of(state).overall == 5)
    }

    @Test("All four components are named, and stress is marked as the one that inverts")
    func stressIsTheOneWhereLowIsGood() throws {
        let state = try Decoding.decode(WellnessState.self, Wellness.reading(date: "2026-03-03"))

        let reading = WellnessReading.of(state)

        #expect(reading.components.map(\.name) == ["Mood", "Energy", "Motivation", "Stress"])
        #expect(reading.components.first { $0.name == "Stress" }?.higherIsBetter == false)
        #expect(reading.components.filter(\.higherIsBetter).count == 3)
    }
}

/// An injury's events, and the one reading that says how it is going.
@Suite("Injury history")
struct InjuryHistoryTests {
    @Test("Events run newest first, and the latest pain is the newest event's")
    func theLatestEventLeads() throws {
        let injury = try Decoding.decode(Injury.self, Tracked.injury(events: """
            [\(Tracked.event(
                id: "dddddddd-dddd-dddd-dddd-ddddddddd001", painLevel: 6,
                createdAt: "2026-02-01T09:00:00")),
             \(Tracked.event(
                id: "dddddddd-dddd-dddd-dddd-ddddddddd003", painLevel: 2,
                createdAt: "2026-03-01T09:00:00")),
             \(Tracked.event(
                id: "dddddddd-dddd-dddd-dddd-ddddddddd002", painLevel: 4,
                createdAt: "2026-02-15T09:00:00"))]
            """))

        let history = InjuryHistory.of(injury)

        #expect(history.events.map(\.painLevel) == [2, 4, 6])
        #expect(history.latestPain == 2)
    }

    @Test("An injury with nothing logged against it has no latest pain, which is not a pain of nought")
    func noEventsMeansNoPainReading() throws {
        let injury = try Decoding.decode(Injury.self, Tracked.injury())

        let history = InjuryHistory.of(injury)

        #expect(history.events.isEmpty)
        #expect(history.latestPain == nil)
    }

    @Test("A treatment and a check-in are different events, read from the wire's integer")
    func eventKindsAreDistinguished() throws {
        let injury = try Decoding.decode(Injury.self, Tracked.injury(events: """
            [\(Tracked.event(
                id: "dddddddd-dddd-dddd-dddd-ddddddddd001", painLevel: 6, kind: 0,
                createdAt: "2026-02-01T09:00:00")),
             \(Tracked.event(
                id: "dddddddd-dddd-dddd-dddd-ddddddddd002", painLevel: 4, kind: 1,
                createdAt: "2026-02-15T09:00:00"))]
            """))

        #expect(injury.events.map(\.kind) == [.treatment, .checkIn])
    }
}

/// The catalogue, which is the only thing that can put a name on a set.
@Suite("Movement catalogue")
struct MovementCatalogueTests {
    private static let bench = "22222222-2222-2222-2222-222222222222"

    @Test("A movement reads as modifier, equipment, base — the order a lifter says them")
    func aMovementReadsAsALifterSaysIt() throws {
        let movement = try Decoding.decode(MovementData.self, Catalogue.movement(
            id: Self.bench, base: "Bench Press", equipment: "Dumbbell", modifier: "Incline"))

        #expect(movement.displayName == "Incline Dumbbell Bench Press")
    }

    @Test("Equipment already in the base's name is not said twice")
    func equipmentIsNotRepeated() throws {
        // "Barbell Barbell Row" is how a naive join reads.
        let movement = try Decoding.decode(MovementData.self, Catalogue.movement(
            id: Self.bench, base: "Barbell Row", equipment: "Barbell"))

        #expect(movement.displayName == "Barbell Row")
    }

    @Test("A set with no movement identity is unspecified, not unknown")
    func noMovementIsNotTheSameAsAnUnresolvedOne() throws {
        // A trail run genuinely has no movement; a lift whose movement the catalogue is missing
        // is a different problem, and they must not read the same way.
        let catalogue = MovementCatalogue(try Decoding.list(
            MovementData.self, "[\(Catalogue.movement(id: Self.bench, base: "Bench Press"))]"))

        #expect(catalogue.name(for: nil) == "Unspecified")
        #expect(catalogue.name(for: UUID()) == "Unknown movement")
        #expect(catalogue.name(for: UUID(uuidString: Self.bench)) == "Barbell Bench Press")
    }

    @Test("An empty catalogue still answers, so a failed catalogue read does not hide a set")
    func anEmptyCatalogueStillNamesSomething() {
        #expect(MovementCatalogue().isEmpty)
        #expect(MovementCatalogue().name(for: UUID()) == "Unknown movement")
    }
}

/// Personal records, and the number the client must print rather than derive.
@Suite("Personal records")
struct PersonalRecordTests {
    private static let bench = "22222222-2222-2222-2222-222222222222"

    @Test("Volume is the server's figure, received rather than multiplied here")
    func volumeIsReceived() throws {
        // Deriving it would give two numbers for one record. The fixture's volume is deliberately
        // not weight times reps, so a client that recomputed it would fail this.
        let record = try Decoding.decode(PersonalRecord.self, Records.record(
            id: "ffffffff-ffff-ffff-ffff-ffffffffff01", movementId: Self.bench,
            kind: 1, weight: 100, reps: 5, volume: 499))

        #expect(record.volume == 499)
        #expect(record.kind == .volume)
    }

    @Test("A movement with only one kind of record is still a row worth showing")
    func oneKindOfRecordIsEnough() throws {
        let summary = try Decoding.decode(MovementRecords.self, Records.summary(
            movementId: Self.bench,
            base: "Bench Press",
            strength: Records.record(
                id: "ffffffff-ffff-ffff-ffff-ffffffffff01", movementId: Self.bench,
                kind: 0, weight: 100, reps: 1, volume: 100)))

        #expect(!summary.isEmpty)
        #expect(summary.strength?.kind == .strength)
        #expect(summary.volume == nil)
    }

    @Test("A movement with neither record is empty, so the screen can drop the row")
    func aSummaryWithNoRecordsIsEmpty() throws {
        let summary = try Decoding.decode(
            MovementRecords.self, Records.summary(movementId: Self.bench, base: "Bench Press"))

        #expect(summary.isEmpty)
    }

    @Test("A first record has no previous one, which is not a gap of zero days")
    func aFirstRecordHasNoPrevious() throws {
        let record = try Decoding.decode(PersonalRecord.self, Records.record(
            id: "ffffffff-ffff-ffff-ffff-ffffffffff01", movementId: Self.bench,
            kind: 0, weight: 100, reps: 1, volume: 100))

        #expect(record.previousAchievedAt == nil)
    }
}
