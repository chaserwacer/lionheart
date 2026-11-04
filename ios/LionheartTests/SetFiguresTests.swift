import Foundation
import Testing

@testable import Lionheart

/// How one set reads as the columns of a set row, for every shape either side can produce.
@Suite("Set figures")
struct SetFiguresTests {
    private static let squat = UUID(uuidString: "11111111-1111-1111-1111-111111111111")!

    private static func measured(_ figures: SetFigures) -> SetFigures.Measured? {
        guard case .measured(let measured) = figures else { return nil }
        return measured
    }

    @Test("A performed lift reads load first, in pounds, then reps, with its RPE")
    func aLiftReadsLoadThenReps() throws {
        let set = try Decoding.decode(
            SetEntry.self, Session.liftSet(reps: 5, weight: 92.5, unit: 0, rpe: "8.5"))

        let figures = try #require(Self.measured(.of(set)))

        // Logged in kilograms: 92.5 kg is 203.9 lb, to the tenth.
        #expect(figures.amount == "203.9")
        #expect(figures.unit == "lb")
        #expect(figures.work == "× 5")
        #expect(figures.extra == nil)
        #expect(figures.rpe == 8.5)
    }

    @Test("An unrated set carries no RPE at all, rather than a zero")
    func anUnratedSetHasNoRpe() throws {
        // `Rpe` is two cases on purpose, and this is where that distinction finally shows: an
        // unrated set must not be drawn as RPE 0, which is a claim the athlete never made.
        let set = try Decoding.decode(SetEntry.self, Session.liftSet(rpe: "null"))

        #expect(Self.measured(.of(set))?.rpe == nil)
    }

    @Test("A quarter-pound load keeps its precision, a round one reads round, and kilograms convert to the tenth")
    func loadsKeepPlatePrecision() throws {
        let quarter = SetCommand.lift(.init(
            movementDataId: Self.squat, reps: 3, weight: 102.25, unit: .pounds, rpe: .unrated))
        let round = try Decoding.decode(SetEntry.self, Session.liftSet(weight: 100, unit: 1))
        let kilograms = try Decoding.decode(SetEntry.self, Session.liftSet(weight: 100, unit: 0))

        #expect(Self.measured(.of(quarter))?.amount == "102.25")
        #expect(Self.measured(.of(round))?.amount == "100")
        #expect(Self.measured(.of(kilograms))?.amount == "220.5")
    }

    @Test("A distance-and-time set reads as the distance, then the clock time")
    func aDistanceTimeSetReadsDistanceThenTime() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"distanceTime","source":{"$type":"formed"},"facets":[],
             "movementDataId":"22222222-2222-2222-2222-222222222222","distance":5,
             "unit":3,"duration":"00:24:10","rpe":7}
            """)

        let figures = try #require(Self.measured(.of(set)))

        // Logged in kilometres, read in miles.
        #expect(figures.amount == "3.11")
        #expect(figures.unit == "mi")
        #expect(figures.work == "24:10")
        #expect(figures.spoken == "3.11 mi in 24:10")
    }

    @Test("A trail run carries its climb as a third figure, signed")
    func aTrailRunCarriesItsClimb() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"trailRun","source":{"$type":"formed"},"facets":[],"distance":12,
             "unit":3,"duration":"01:18:00","elevationGainMeters":420,"rpe":null}
            """)
        let descent = SetCommand.trailRun(.init(
            distance: 8, unit: .kilometers, duration: 2400, elevationGainMeters: -150, rpe: .unrated))

        #expect(Self.measured(.of(set))?.work == "1:18:00")
        #expect(Self.measured(.of(set))?.extra == "+1378 ft")
        #expect(Self.measured(.of(descent))?.extra == "−492 ft")
    }

    @Test("A freeform set reads as the words it was logged with, on either side")
    func aFreeformSetKeepsItsWords() throws {
        let performed = try Decoding.decode(SetEntry.self, """
            {"$type":"freeform","source":{"$type":"texted","rawText":"carried logs uphill",
              "structureConfidence":0},"facets":[]}
            """)

        #expect(SetFigures.of(performed) == .described("carried logs uphill"))
        #expect(SetFigures.of(SetCommand.freeform(text: "hip mobility")) == .described("hip mobility"))
    }

    @Test("A freeform set logged with no words is described as having none, not as blank")
    func aWordlessFreeformSetIsNamedAsSuch() throws {
        let performed = try Decoding.decode(SetEntry.self, """
            {"$type":"freeform","source":{"$type":"formed"},"facets":[]}
            """)

        #expect(SetFigures.of(SetCommand.freeform(text: "  ")) == .undescribed)
        #expect(SetFigures.of(performed) == .undescribed)
    }

    @Test("A set shape this build cannot read still shows the athlete's own words")
    func anUnknownSetShapeFallsBackToItsWords() throws {
        // The shape is unknown; the words are not. Losing them would lose the only record of the
        // set the athlete actually made.
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"isometric","source":{"$type":"texted","rawText":"90s plank",
              "structureConfidence":0},"facets":[]}
            """)

        #expect(SetFigures.of(set) == .described("90s plank"))
    }

    @Test("A set shape this build cannot read, with no words, says so rather than showing nothing")
    func anUnknownSetShapeWithoutWordsIsNamed() throws {
        let set = try Decoding.decode(SetEntry.self, """
            {"$type":"isometric","source":{"$type":"formed"},"facets":[]}
            """)

        #expect(SetFigures.of(set) == .described("A set shape this version cannot read (isometric)"))
    }
}

/// Whether a logged set met its prescription, which decides whether its row repeats the target.
@Suite("Target attainment")
struct TargetAttainmentTests {
    private static let squat = UUID(uuidString: "11111111-1111-1111-1111-111111111111")!

    private static let fiveAt145 = SetPrescription.lift(.init(
        movementDataId: squat, targetReps: 5, targetWeight: 145, unit: .kilograms, targetRpe: .rated(8)))

    private static func lift(_ reps: Int, _ weight: Double, _ unit: WeightUnit = .kilograms) -> SetCommand {
        .lift(.init(movementDataId: squat, reps: reps, weight: weight, unit: unit, rpe: .unrated))
    }

    @Test(
        "A lift meets its target at the prescribed reps and load or above",
        arguments: [(5, 145.0), (6, 145.0), (5, 150.0)])
    func meetingOrBeatingTheTargetMeetsIt(reps: Int, weight: Double) {
        #expect(Self.fiveAt145.isMet(by: Self.lift(reps, weight)))
    }

    @Test(
        "A lift short on reps or load misses its target",
        arguments: [(4, 145.0), (5, 142.5), (3, 150.0)])
    func fallingShortMissesTheTarget(reps: Int, weight: Double) {
        #expect(!Self.fiveAt145.isMet(by: Self.lift(reps, weight)))
    }

    @Test("A lift in the other unit never meets the target, because weights never convert")
    func anotherUnitIsNeverAMatch() {
        #expect(!Self.fiveAt145.isMet(by: Self.lift(5, 400, .pounds)))
    }

    @Test("A timed target is met by at least the distance in at most the time")
    func aTimedTargetWantsDistanceAndPace() {
        let target = SetPrescription.distanceTime(.init(
            movementDataId: Self.squat, targetDistance: 5, unit: .kilometers, targetDuration: 1500))
        func run(_ distance: Double, _ duration: TimeInterval) -> SetCommand {
            .distanceTime(.init(
                movementDataId: Self.squat, distance: distance, unit: .kilometers, duration: duration,
                rpe: .unrated))
        }

        #expect(target.isMet(by: run(5, 1500)))
        #expect(target.isMet(by: run(5.2, 1440)))
        #expect(!target.isMet(by: run(5, 1560)))
        #expect(!target.isMet(by: run(4.8, 1400)))
    }

    @Test("A set of another shape, or a target in words, is never taken as met")
    func otherShapesAreNeverMatches() {
        #expect(!Self.fiveAt145.isMet(by: .freeform(text: "5 × 145")))
        #expect(!SetPrescription.freeform(text: "heavy triple").isMet(by: Self.lift(3, 160)))
        #expect(!SetPrescription.unrecognised("cluster").isMet(by: Self.lift(3, 160)))
    }
}
