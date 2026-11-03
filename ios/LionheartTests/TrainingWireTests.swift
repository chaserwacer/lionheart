import Foundation
import Testing

@testable import Lionheart

/// What goes on the wire for a set, and what comes back.
///
/// The fixtures are the **live server's** own bytes, taken from a session driven through
/// `POST /active`, `PUT /sets` and `POST /complete` against a running host. That matters: the
/// OpenAPI document describes the C# types and not the converters that actually run, and it is
/// wrong about `rpe` — which is a bare number or null here, not the `{isRated, value}` the document
/// shows.
@Suite("Set command on the wire")
struct SetCommandWireTests {
    private static func encoded(_ command: SetCommand) throws -> [String: Any] {
        let data = try JSONEncoder().encode(command)
        return try JSONSerialization.jsonObject(with: data) as? [String: Any] ?? [:]
    }

    @Test("A lift encodes as the server's lift command")
    func aLiftEncodes() throws {
        let json = try Self.encoded(.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 100,
            unit: .kilograms, rpe: .rated(8))))

        #expect(json["$type"] as? String == "lift")
        #expect(json["movementDataId"] as? String == AWorkout.bench.uuidString)
        #expect(json["reps"] as? Int == 5)
        #expect(json["weight"] as? Double == 100)
        // Integer-valued enums, not names.
        #expect(json["unit"] as? Int == 0)
        // A bare number, which is what `RpeJsonConverter` writes and reads.
        #expect(json["rpe"] as? Double == 8)
    }

    @Test("An unrated set sends a null RPE rather than omitting it or sending zero")
    func anUnratedSetSendsNull() throws {
        let json = try Self.encoded(.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 100,
            unit: .kilograms, rpe: .unrated)))

        // Unrated is a case, and the server's `Stated.Rating` reads null as `Rpe.Unrated`. Sending
        // zero would record a set the athlete said was effortless.
        #expect(json["rpe"] is NSNull)
    }

    @Test("Pounds encode as the server's second weight unit")
    func poundsEncodeAsOne() throws {
        let json = try Self.encoded(.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 225,
            unit: .pounds, rpe: .unrated)))

        #expect(json["unit"] as? Int == 1)
    }

    @Test("Distance work sends a TimeSpan, not a number of seconds")
    func durationEncodesAsATimeSpan() throws {
        let json = try Self.encoded(.distanceTime(SetCommand.DistanceTime(
            movementDataId: AWorkout.treadmill, distance: 5, unit: .kilometers,
            duration: 1470, rpe: .unrated)))

        #expect(json["$type"] as? String == "distanceTime")
        #expect(json["duration"] as? String == "00:24:30")
        #expect(json["unit"] as? Int == 3)
    }

    @Test("A trail run sends its elevation and no movement, which it has none of")
    func aTrailRunEncodes() throws {
        let json = try Self.encoded(.trailRun(SetCommand.TrailRun(
            distance: 10, unit: .kilometers, duration: 3900,
            elevationGainMeters: 320, rpe: .rated(7))))

        #expect(json["$type"] as? String == "trailRun")
        #expect(json["elevationGainMeters"] as? Double == 320)
        #expect(json["movementDataId"] == nil)
    }

    @Test("A freeform set sends its words, which are the whole record")
    func freeformEncodesItsText() throws {
        let json = try Self.encoded(.freeform(text: "ten minutes of hip mobility"))

        #expect(json["$type"] as? String == "freeform")
        #expect(json["text"] as? String == "ten minutes of hip mobility")
    }

    @Test("An unrecognised unit is sent back as the number it arrived as")
    func anUnknownUnitKeepsItsNumber() throws {
        // A set this build could not name must not silently become kilograms on the way back.
        let json = try Self.encoded(.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 100,
            unit: .unrecognised(7), rpe: .unrated)))

        #expect(json["unit"] as? Int == 7)
    }

    @Test("Every shape survives the document round trip")
    func everyShapeRoundTrips() throws {
        let commands: [SetCommand] = [
            .lift(SetCommand.Lift(
                movementDataId: AWorkout.bench, reps: 5, weight: 102.5,
                unit: .pounds, rpe: .rated(8.5))),
            .distanceTime(SetCommand.DistanceTime(
                movementDataId: AWorkout.treadmill, distance: 5.25, unit: .miles,
                duration: 1470, rpe: .unrated)),
            .trailRun(SetCommand.TrailRun(
                distance: 10, unit: .kilometers, duration: 3900,
                elevationGainMeters: -40, rpe: .rated(7))),
            .freeform(text: "some words"),
        ]

        let data = try JSONEncoder().encode(commands)

        #expect(try JSONDecoder().decode([SetCommand].self, from: data) == commands)
    }

    @Test("A set shape this build cannot name is refused rather than read as something else")
    func anUnknownShapeThrows() {
        // There is no `.unrecognised` case here, deliberately: nothing but this client writes one,
        // so a strange discriminator means a corrupt document and the whole document reads as
        // absent rather than as a workout missing a set.
        #expect(throws: (any Error).self) {
            try JSONDecoder().decode(
                SetCommand.self, from: Data(#"{"$type":"isometricHold","seconds":30}"#.utf8))
        }
    }

    @Test("A lift with no movement id is refused, because the athlete's set would be a lift of air")
    func aLiftWithoutAMovementThrows() {
        #expect(throws: (any Error).self) {
            try JSONDecoder().decode(
                SetCommand.self, from: Data(#"{"$type":"lift","reps":5,"weight":100}"#.utf8))
        }
    }
}

/// The command bodies the session routes take.
@Suite("Session request bodies")
struct SessionRequestWireTests {
    /// A moment with a known, non-UTC offset, so the two spellings cannot be confused.
    private static let timeZone = TimeZone(secondsFromGMT: -5 * 3600)!
    private static let evening = Date(timeIntervalSince1970: 1_793_491_200)

    @Test("An instant for a DateTimeOffset carries the device's own offset")
    func offsetWireKeepsTheOffset() {
        // The server reads a session's calendar day at the offset it was sent with. The same
        // instant as `Z` is the next morning, and the session would land on the wrong day — with
        // no symptom at all until the clock goes past evening.
        let written = WireDate.offsetWire(from: Self.evening, in: Self.timeZone)

        #expect(written.hasSuffix("-05:00"))
        #expect(written.hasPrefix("2026-10-31T19:00:00"))
    }

    @Test("The window's bounds stay UTC, because they are compared against stored instants")
    func windowBoundsAreUtc() {
        #expect(WireDate.wire(from: Self.evening).hasSuffix("Z"))
    }

    @Test("A period with an end is an interval; without one it is an instant")
    func aPeriodWithNoEndIsAnInstant() throws {
        let instant = try JSONSerialization.jsonObject(
            with: try JSONEncoder().encode(PeriodRequest(start: Self.evening, end: nil)))
            as? [String: Any] ?? [:]

        #expect(instant["start"] is String)
        // Absent rather than null: a missing end is what tells the server this was a moment, and
        // the flat shape is deliberate on its side — an interval whose end precedes its start
        // throws inside the formatter and would escape as a 500.
        #expect(instant["end"] == nil)
    }

    @Test("A finish that lands before the start collapses to an instant rather than a 400")
    func aBackwardsPeriodCollapses() {
        // A device clock that moved backwards would otherwise be refused with a validation error
        // the athlete cannot act on.
        let period = PeriodRequest.interval(
            from: Self.evening, to: Self.evening.addingTimeInterval(-60))

        #expect(period.end == nil)
    }

    @Test("A period from a start to a later finish keeps both")
    func aForwardPeriodKeepsItsEnd() {
        let end = Self.evening.addingTimeInterval(3600)

        #expect(PeriodRequest.interval(from: Self.evening, to: end).end == end)
    }

    @Test("An effort with every rating left alone is empty, which means not reported")
    func anEmptyEffortIsEmpty() {
        #expect(PerceivedEffortRequest.unrated.isEmpty)
        #expect(!PerceivedEffortRequest(
            accumulatedFatigue: 3, difficultyRating: nil,
            engagementRating: nil, externalVariablesRating: nil).isEmpty)
    }
}

/// The movement catalogue, whose keys are spelled the pre-remodel way.
@Suite("Movement decoding")
struct MovementDecodingTests {
    /// The live server's own answer to `GET /api/movement-datas`, trimmed to one row.
    private static let payload = Data(#"""
        [{"movementDataID":"22222222-2222-2222-2222-222222222222",
          "equipmentID":"4d1baac8-b0eb-4e92-8eaf-b1c0ba1d8a1a",
          "equipment":{"equipmentID":"4d1baac8-b0eb-4e92-8eaf-b1c0ba1d8a1a",
                       "name":"Dumbbell","enabled":true},
          "movementBaseID":"726424c4-7262-4b17-b55b-93eabbaefe6f",
          "movementBase":{"movementBaseID":"726424c4-7262-4b17-b55b-93eabbaefe6f",
                          "name":"Bench press","description":"","muscleGroups":[]},
          "movementModifierID":"5e1b4f0a-0000-0000-0000-000000000000",
          "movementModifier":{"movementModifierID":"5e1b4f0a-0000-0000-0000-000000000000",
                              "name":"incline"}}]
        """#.utf8)

    @Test("A movement reads its base, equipment and modifier")
    func aMovementDecodes() throws {
        let movements = try JSONDecoder().decode(LenientList<MovementData>.self, from: Self.payload)

        let movement = try #require(movements.elements.first)
        #expect(movement.movementDataId == AWorkout.bench)
        // The server lower-cases a modifier's name on the way in, so its first letter is the one
        // thing fixed here. The base keeps whatever case the server holds — this fixture stores
        // "Bench press", so that is what shows; a base stored "Bench Press" would show that.
        // Rewriting the base's case would be this client overruling the athlete's own naming.
        #expect(movement.title == "Incline Bench press")
        #expect(movement.qualifier == "Dumbbell")
    }

    @Test("A movement with no modifier reads as its base alone")
    func noModifierIsJustTheBase() throws {
        let payload = Data(#"""
            {"movementDataID":"22222222-2222-2222-2222-222222222222",
             "equipment":{"name":"Barbell"},
             "movementBase":{"name":"Back squat"},
             "movementModifier":null}
            """#.utf8)

        let movement = try JSONDecoder().decode(MovementData.self, from: payload)

        #expect(movement.title == "Back squat")
    }

    @Test("A movement with no id is the one row worth refusing")
    func noIdIsRefused() {
        #expect(throws: (any Error).self) {
            try JSONDecoder().decode(
                MovementData.self, from: Data(#"{"equipment":{"name":"Barbell"}}"#.utf8))
        }
    }

    @Test("Searching matches each word separately, ignoring case and accents")
    func searchMatchesWordByWord() throws {
        let movement = try JSONDecoder().decode(
            LenientList<MovementData>.self, from: Self.payload).elements[0]

        // Typing two half-words is faster than getting one whole phrase right.
        #expect(movement.matches("bench db") == false)
        #expect(movement.matches("bench dumb"))
        #expect(movement.matches("INCLINE"))
        #expect(movement.matches("incline bench"))
        #expect(movement.matches(""))
        #expect(!movement.matches("squat"))
    }

    @Test("A record summary reads the two figures a workout needs")
    func recordSummariesDecode() throws {
        // A record carries its own id and its unit, both of which the comparison depends on: the
        // server stores a weight's unit and compares numerically without converting.
        let payload = Data(#"""
            [{"movementDataID":"22222222-2222-2222-2222-222222222222",
              "strengthPR":{"personalRecordID":"33333333-3333-3333-3333-333333333333",
                            "movementDataID":"22222222-2222-2222-2222-222222222222",
                            "prType":0,"weight":110.0,"weightUnit":0,"reps":3,"volume":330.0},
              "volumePR":{"personalRecordID":"44444444-4444-4444-4444-444444444444",
                          "movementDataID":"22222222-2222-2222-2222-222222222222",
                          "prType":1,"weight":100.0,"weightUnit":0,"reps":10,"volume":1000.0},
              "lastPRDate":"2026-10-01T00:00:00"}]
            """#.utf8)

        let records = try JSONDecoder()
            .decode(LenientList<MovementRecords>.self, from: payload).elements

        #expect(records[0].strength?.weight == 110)
        #expect(records[0].strength?.unit == .kilograms)
        #expect(records[0].volume?.volume == 1000)
        #expect(!records[0].isEmpty)
    }

    @Test("A movement the server holds no record for reads as empty, not as zero")
    func anAbsentRecordIsEmpty() throws {
        let payload = Data(#"""
            {"movementDataID":"22222222-2222-2222-2222-222222222222",
             "strengthPR":null,"volumePR":null,"lastPRDate":null}
            """#.utf8)

        let records = try JSONDecoder().decode(MovementRecords.self, from: payload)

        #expect(records.isEmpty)
        #expect(records.strength == nil)
        #expect(records.volume == nil)
    }
}
