import Foundation
import Testing

@testable import Lionheart

/// What the athlete typed, and whether it amounts to a set.
///
/// The claims here are about the gap between text and numbers, which is where a logging screen
/// either feels like typing or fights back.
@Suite("Set draft")
struct SetDraftTests {
    private static func aLiftDraft(reps: String, weight: String) -> SetDraft {
        var draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 0, weight: 0,
            unit: .kilograms, rpe: .unrated)))
        draft.reps = reps
        draft.weight = weight
        return draft
    }

    @Test("A draft seeded from a set reproduces that set")
    func aDraftRoundTripsASet() {
        let original = SetCommand.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 102.5,
            unit: .pounds, rpe: .rated(8.5)))

        #expect(SetDraft(from: original).command == original)
    }

    @Test("A distance-time set round-trips through two duration fields")
    func aDistanceSetRoundTrips() {
        let original = SetCommand.distanceTime(SetCommand.DistanceTime(
            movementDataId: AWorkout.treadmill, distance: 5, unit: .kilometers,
            duration: 1470, rpe: .rated(6.5)))

        let draft = SetDraft(from: original)

        #expect(draft.minutes == "24")
        #expect(draft.seconds == "30")
        #expect(draft.command == original)
    }

    @Test("A trail run round-trips, elevation and all")
    func aTrailRunRoundTrips() {
        let original = SetCommand.trailRun(SetCommand.TrailRun(
            distance: 10.5, unit: .miles, duration: 3900,
            elevationGainMeters: 320, rpe: .unrated))

        #expect(SetDraft(from: original).command == original)
    }

    @Test("A zero comes through as an empty field, not as a nought to clear")
    func zeroSeedsAnEmptyField() {
        let draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 0, weight: 0,
            unit: .kilograms, rpe: .unrated)))

        #expect(draft.weight.isEmpty)
        #expect(draft.reps.isEmpty)
        #expect(!draft.isLoggable)
    }

    @Test("A lift with no reps has not happened")
    func aLiftNeedsReps() {
        #expect(!Self.aLiftDraft(reps: "", weight: "100").isLoggable)
        #expect(!Self.aLiftDraft(reps: "0", weight: "100").isLoggable)
    }

    @Test("A lift with no weight has not happened either")
    func aLiftNeedsAWeight() {
        #expect(!Self.aLiftDraft(reps: "5", weight: "").isLoggable)
    }

    @Test("A bodyweight set of no added load is a weight of zero, which is loggable")
    func zeroWeightTypedIsLoggable() {
        // Typing a nought is the athlete saying "no added weight" — quite different from leaving
        // the field untouched.
        let draft = Self.aLiftDraft(reps: "10", weight: "0")

        #expect(draft.isLoggable)
        #expect(draft.command?.liftPart?.weight == 0)
    }

    @Test("Half-typed text is simply not a set yet, and says nothing about it")
    func halfTypedTextIsNotASet() {
        #expect(!Self.aLiftDraft(reps: "5", weight: ".").isLoggable)
        #expect(!Self.aLiftDraft(reps: "5", weight: "-").isLoggable)
    }

    @Test("Either decimal separator is accepted")
    func bothDecimalSeparatorsWork() {
        // The decimal keyboard shows the athlete's own locale's separator, so a comma is what a
        // European athlete will type.
        #expect(SetDraft.number("102.5") == 102.5)
        #expect(SetDraft.number("102,5") == 102.5)
        #expect(SetDraft.number(" 102.5 ") == 102.5)
        #expect(SetDraft.number("") == nil)
        #expect(SetDraft.number("heavy") == nil)
    }

    @Test("Reps are whole, however they were typed")
    func repsAreWhole() {
        #expect(SetDraft.whole("5") == 5)
        #expect(SetDraft.whole("5.4") == 5)
        #expect(SetDraft.whole("5.6") == 6)
    }

    @Test("A duration adds up from the two fields, either of which may be empty")
    func durationAddsUp() {
        var draft = SetDraft(from: .distanceTime(SetCommand.DistanceTime(
            movementDataId: AWorkout.treadmill, distance: 5, unit: .kilometers,
            duration: 0, rpe: .unrated)))

        draft.minutes = "24"
        draft.seconds = "30"
        #expect(draft.duration == 1470)

        draft.seconds = ""
        #expect(draft.duration == 1440)

        draft.minutes = ""
        draft.seconds = "45"
        #expect(draft.duration == 45)
    }

    @Test("Distance work with no distance is not a set")
    func distanceWorkNeedsADistance() {
        var draft = SetDraft(from: .distanceTime(SetCommand.DistanceTime(
            movementDataId: AWorkout.treadmill, distance: 0, unit: .kilometers,
            duration: 0, rpe: .unrated)))
        draft.minutes = "24"

        #expect(!draft.isLoggable)
    }

    @Test("Distance work with no time is a set — a distance covered is the record")
    func distanceWorkDoesNotNeedATime() {
        var draft = SetDraft(from: .distanceTime(SetCommand.DistanceTime(
            movementDataId: AWorkout.treadmill, distance: 0, unit: .kilometers,
            duration: 0, rpe: .unrated)))
        draft.distance = "5"

        #expect(draft.isLoggable)
        #expect(draft.command?.coveredPart?.duration == 0)
    }

    @Test("A lift with no movement cannot be logged, because a lift is of something")
    func aLiftNeedsAMovement() {
        var draft = Self.aLiftDraft(reps: "5", weight: "100")
        draft.movementDataId = nil

        #expect(!draft.isLoggable)
    }

    @Test("A trail run needs no movement, because the domain gives it none")
    func aTrailRunNeedsNoMovement() {
        var draft = SetDraft(from: .trailRun(SetCommand.TrailRun(
            distance: 0, unit: .kilometers, duration: 0,
            elevationGainMeters: 0, rpe: .unrated)))
        draft.distance = "10"

        #expect(draft.movementDataId == nil)
        #expect(draft.isLoggable)
    }

    @Test("Words with nothing in them are not a set")
    func wordsMustSaySomething() {
        var draft = SetDraft(from: .freeform(text: ""))
        #expect(!draft.isLoggable)

        draft.text = "   "
        #expect(!draft.isLoggable)

        draft.text = "  ten minutes of mobility  "
        #expect(draft.command == .freeform(text: "ten minutes of mobility"))
    }

    @Test("An unrated RPE stays unrated — it is a case, never a zero")
    func rpeIsNeverInsistedOn() {
        let draft = Self.aLiftDraft(reps: "5", weight: "100")

        #expect(draft.rpe == .unrated)
        #expect(draft.command?.rpe == .unrated)
    }
}
