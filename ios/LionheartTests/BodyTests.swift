import Foundation
import Testing

@testable import Lionheart

/// What the Body tab reads out of three windows.
@Suite("Body snapshot")
struct BodySnapshotTests {
    @Test("Oura days and wellness entries run newest first, the order they are read in")
    func recordsAreNewestFirst() throws {
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-01", readinessScore: "70")),
             \(Oura.day(date: "2026-03-03", readinessScore: "80")),
             \(Oura.day(date: "2026-03-02", readinessScore: "75"))]
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

    @Test("The latest readiness follows exactly the rule Today uses")
    func readinessReusesTodaysRule() throws {
        // One rule, one answer: two screens disagreeing about the athlete's recovery would be two
        // different claims about his body. A zero is a score Oura issued, so the most recent day
        // wins even when that score is nought.
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-03", readinessScore: "0")),
             \(Oura.day(date: "2026-03-01", readinessScore: "72"))]
            """)

        let today = CalendarDay(year: 2026, month: 3, day: 4)
        let snapshot = BodySnapshot.reading(ouraDays: days, wellness: [], injuries: [])
        let readiness = snapshot.latestRecovery(today: today)?.readiness

        #expect(readiness == TodaySnapshot.reading(sessions: [], ouraDays: days, today: today).readiness)
        #expect(readiness?.score == OuraScore(0))
        #expect(readiness?.day == CalendarDay(year: 2026, month: 3, day: 3))
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
        let day = try Decoding.decode(
            OuraDay.self,
            Oura.day(date: "2026-03-03", readinessScore: "80", resilience: Oura.resilience))

        #expect(OuraPanel.panels(of: day).map(\.title)
            == ["Readiness", "Sleep", "Activity", "Stress", "Blood oxygen", "Resilience"])
    }

    @Test("Only the scored collections have a score; the others have measurements")
    func onlySomeCollectionsAreScored() throws {
        // A missing score and a score of zero are different facts, which is why this is optional
        // rather than a zero standing in for "Oura does not score this".
        let day = try Decoding.decode(
            OuraDay.self,
            Oura.day(date: "2026-03-03", readinessScore: "80", resilience: Oura.resilience))
        let panels = OuraPanel.panels(of: day)

        #expect(panels.first { $0.title == "Readiness" }?.score == OuraScore(80))
        #expect(panels.first { $0.title == "Stress" }?.score == nil)
        #expect(panels.first { $0.title == "Blood oxygen" }?.score == nil)
        #expect(panels.first { $0.title == "Resilience" }?.score == nil)
    }

    @Test("Every panel says what it shows besides its score")
    func everyPanelHasFigures() throws {
        // Exhaustive over the panels, so a collection added as the Oura surface widens has to
        // decide what it shows rather than rendering as a bare score.
        let day = try Decoding.decode(
            OuraDay.self,
            Oura.day(date: "2026-03-03", readinessScore: "80", resilience: Oura.resilience))

        for panel in OuraPanel.panels(of: day) {
            #expect(!panel.figures.isEmpty)
        }
    }

    @Test("A figure Oura did not send is left out rather than printed as a dash")
    func absentFiguresAreOmitted() throws {
        // The panel is the set of things that were measured, so a null contributor is simply not
        // a row — not a row with nothing in it.
        let day = try Decoding.decode(OuraDay.self, """
            {"day":"2026-03-03","activity":null,"readiness":null,"sleep":null,
             "spo2":{"averageSpo2Percentage":96.5,"breathingDisturbanceIndex":null},
             "stress":null,"resilience":null}
            """)

        let spo2 = try #require(OuraPanel.panels(of: day).first)

        #expect(spo2.figures.map(\.label) == ["Average SpO₂"])
    }
}

/// The night, the workouts and the tags the Body screen hangs off a day.
@Suite("Oura events on a day")
struct OuraEventGroupingTests {
    private static let day = CalendarDay(year: 2026, month: 3, day: 4)

    @Test("The night is the period Oura flagged as the main sleep, not the longest one")
    func theMainSleepIsOurasChoice() throws {
        // Taking the longest would pick a long afternoon nap over a broken night.
        let periods = try Decoding.list(OuraEvent<OuraSleepPeriod>.self, """
            [\(Oura.sleepPeriod(
                id: "eeeeeeee-0000-0000-0000-0000000000b1", day: "2026-03-04",
                start: "2026-03-04T13:00:00+00:00", end: "2026-03-04T16:00:00+00:00",
                isMainSleep: false)),
             \(Oura.sleepPeriod(
                id: "eeeeeeee-0000-0000-0000-0000000000b2", day: "2026-03-04",
                start: "2026-03-03T23:10:00+00:00", end: "2026-03-04T07:10:00+00:00",
                isMainSleep: true))]
            """)

        let snapshot = BodySnapshot.reading(
            ouraDays: [], wellness: [], injuries: [], nights: periods)

        #expect(snapshot.mainSleep(on: Self.day)?.isMainSleep == true)
    }

    @Test("A day with only naps has no main sleep, which is not the same as no sleep at all")
    func napsAreNotANight() throws {
        let periods = try Decoding.list(OuraEvent<OuraSleepPeriod>.self, """
            [\(Oura.sleepPeriod(
                id: "eeeeeeee-0000-0000-0000-0000000000b3", day: "2026-03-04",
                start: "2026-03-04T13:00:00+00:00", end: "2026-03-04T13:40:00+00:00",
                isMainSleep: false))]
            """)

        let snapshot = BodySnapshot.reading(
            ouraDays: [], wellness: [], injuries: [], nights: periods)

        #expect(snapshot.mainSleep(on: Self.day) == nil)
        #expect(snapshot.nights[Self.day]?.count == 1)
    }

    @Test("Events are grouped by the day Oura filed them under, not by when they started")
    func eventsGroupByOurasDay() throws {
        // The night below begins on the 3rd and is filed under the 4th. Grouping by the clock
        // would move a third of this athlete's nights.
        let periods = try Decoding.list(OuraEvent<OuraSleepPeriod>.self, """
            [\(Oura.sleepPeriod(
                id: "eeeeeeee-0000-0000-0000-0000000000b4", day: "2026-03-04",
                start: "2026-03-03T23:10:00+00:00", end: "2026-03-04T07:10:00+00:00"))]
            """)

        let snapshot = BodySnapshot.reading(
            ouraDays: [], wellness: [], injuries: [], nights: periods)

        #expect(snapshot.nights[Self.day]?.count == 1)
        #expect(snapshot.nights[CalendarDay(year: 2026, month: 3, day: 3)] == nil)
    }

    @Test("Workouts and tags hang off their day too")
    func workoutsAndTagsAreGrouped() throws {
        let workouts = try Decoding.list(
            OuraEvent<OuraWorkout>.self,
            "[\(Oura.workout(id: "eeeeeeee-0000-0000-0000-0000000000b5", day: "2026-03-04"))]")
        let tags = try Decoding.list(
            OuraEvent<OuraTag>.self,
            "[\(Oura.tag(id: "eeeeeeee-0000-0000-0000-0000000000b6", day: "2026-03-04"))]")

        let snapshot = BodySnapshot.reading(
            ouraDays: [], wellness: [], injuries: [], ringWorkouts: workouts, tags: tags)

        #expect(snapshot.workouts(on: Self.day).map(\.activity) == ["walking"])
        #expect(snapshot.tags(on: Self.day).flatMap(\.phrases) == ["Sauna"])
        #expect(snapshot.workouts(on: CalendarDay(year: 2026, month: 3, day: 5)).isEmpty)
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

    @Test("A movement reads as the modifier qualifying the base, with its equipment after")
    func aMovementReadsAsALifterSaysIt() throws {
        let movement = try Decoding.decode(MovementData.self, Catalogue.movement(
            id: Self.bench, base: "Bench Press", equipment: "Dumbbell", modifier: "Incline"))

        // The server lower-cases a modifier on the way in, so only its first letter is fixed —
        // everything else keeps the capitalisation the server holds. `title` and `qualifier` are
        // what a screen with room for two shows; `displayName` is for one.
        #expect(movement.title == "Incline Bench Press")
        #expect(movement.qualifier == "Dumbbell")
        #expect(movement.displayName == "Incline Dumbbell Bench Press")
    }

    @Test("Equipment already in the name is not said twice")
    func equipmentIsNotRepeated() throws {
        // "Barbell row, barbell" is how a naive join reads.
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
