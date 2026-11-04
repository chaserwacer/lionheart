import Foundation
import Testing

@testable import Lionheart

/// Every figure reads in pounds, miles, feet and °F, converted at the words and never in what is
/// stored: what a set logged in kilograms or kilometres says, and how a record's distance is named.
@Suite("Imperial units")
struct ImperialTests {
    @Test("A kilometre distance reads in miles; a track's metres and yards read as logged")
    func distancesReadAsAUSRunnerReadsThem() {
        #expect(Formatting.distance(5, .kilometers) == "3.11 mi")
        #expect(Formatting.distance(400, .meters) == "400 m")
        #expect(Formatting.distance(100, .yards) == "100 yd")
        #expect(Formatting.distance(2.5, .miles) == "2.5 mi")
    }

    @Test("A weight logged in kilograms reads in pounds to the tenth; one in pounds keeps its quarter")
    func weightsReadInPounds() {
        #expect(Formatting.weight(100, .kilograms) == "220.5 lb")
        #expect(Formatting.weight(2.25, .pounds) == "2.25 lb")
        #expect(Formatting.signedWeight(-1, .kilograms) == "−2.2 lb")
        #expect(Formatting.signedWeight(0, .pounds) == "±0 lb")
        // A unit this build cannot name keeps its number rather than being called pounds.
        #expect(Formatting.weight(40, .unrecognised(9)) == "40")
    }

    @Test("A session's volume is one figure in pounds, a part lifted in kilograms converted into it")
    func volumeIsOnePoundsTotal() {
        let totals = WorkoutTotals(
            setCount: 6,
            weights: [
                .init(unit: .pounds, volume: 3_000, reps: 15, setCount: 3),
                .init(unit: .kilograms, volume: 1_000, reps: 15, setCount: 3),
            ],
            distances: [],
            movingDuration: 0)

        // 1,000 kg of volume is 2,204.6 lb.
        #expect(Formatting.totals(totals) == "6 sets · 5,204.6 lb")
    }

    @Test("A fastest time at a race distance goes by the race's name; any other distance by miles")
    func raceDistancesAreNamed() {
        let mile = Achievement.Distance(distance: 1, unit: .miles, duration: 420)
        let fiveK = Achievement.Distance(distance: 5, unit: .kilometers, duration: 1_500)
        let kilometre = Achievement.Distance(distance: 1, unit: .kilometers, duration: 240)

        #expect(Formatting.raceDistance(bracketMeters: 1609, mile) == "Mile")
        #expect(Formatting.raceDistance(bracketMeters: 5000, fiveK) == "5K")
        #expect(Formatting.raceDistance(bracketMeters: 1000, kilometre) == "0.62 mi")
    }

    @Test("A record's pace reads per mile whatever unit the effort was logged in")
    func recordPaceIsPerMile() {
        let pace = Achievement.Pace(
            perKilometer: 300, perMile: 482.8, effort: .init(distance: 10, unit: .kilometers, duration: 3_000))

        #expect(Formatting.pace(pace) == "8:03 /mi")
    }

    @Test("Oura's temperature deviation reads in °F by the scale alone")
    func temperatureReadsInFahrenheit() {
        let day = ARecovery.day(CalendarDay(year: 2026, month: 3, day: 9), temperature: -0.5)

        let value = RecoveryMetric.temperatureDeviation.value(day: day, night: nil)

        #expect(value == -0.9)
        #expect(Formatting.figureText(-0.9, of: .temperatureDeviation) == "−0.9 °F")
    }

    @Test("A climb typed in feet is sent as metres, and one left alone is sent back as it came")
    func climbIsTypedInFeet() {
        let original = SetCommand.trailRun(.init(
            distance: 6, unit: .miles, duration: 3_600, elevationGainMeters: 320, rpe: .unrated))
        var draft = SetDraft(from: original)

        #expect(draft.elevationGainFeet == "1050")
        #expect(draft.command == original)

        draft.elevationGainFeet = "1000"
        guard case .trailRun(let typed)? = draft.command else {
            Issue.record("A trail run with a distance is a set")
            return
        }
        #expect(abs(typed.elevationGainMeters - 304.8) < 0.000_1)
    }

    @Test("A new lift starts in pounds and new distance work in miles")
    func newWorkStartsImperial() {
        let lift = SetDefaults.nextSet(on: AWorkout.bench, in: AWorkout.live())
        let run = CardioWork.outdoorRun.nextSet(in: AWorkout.live())

        #expect(lift.liftPart?.unit == .pounds)
        guard case .trailRun(let trail) = run else {
            Issue.record("The next trail run is a trail run")
            return
        }
        #expect(trail.unit == .miles)
    }
}
