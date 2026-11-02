import Foundation
import Testing

@testable import Lionheart

/// The scalar spellings the server actually uses, which are not the ones the OpenAPI document
/// describes for every type.
@Suite("Wire values")
struct WireValueTests {
    @Test("RPE arrives as a bare number or null, not as an object")
    func rpeIsANumberOrNull() throws {
        // The OpenAPI document describes Rpe as `{isRated, value}` — the schema generator reads the
        // C# struct's properties and does not know about RpeJsonConverter, which writes a number or
        // null. The converter is what actually runs, so it is what the client decodes.
        let rated = try Decoding.decode(TrainingSession.self, Session.completed(
            sets: "[\(Session.liftSet(rpe: "8.5"))]"))
        let unrated = try Decoding.decode(TrainingSession.self, Session.completed(
            sets: "[\(Session.liftSet(rpe: "null"))]"))

        #expect(rated.sets.first?.rpe == .rated(8.5))
        #expect(unrated.sets.first?.rpe == .unrated)
    }

    @Test("An unrated RPE is a case, not a zero")
    func unratedIsNotZero() {
        // Rendering an unrated set as RPE 0 would claim the athlete called it effortless.
        #expect(Rpe(wire: nil) == .unrated)
        #expect(Rpe(wire: nil).value == nil)
        #expect(Rpe(wire: 0) == .rated(0))
    }

    @Test(
        "An RPE outside the server's range is clamped rather than discarding the set",
        arguments: [(-3.0, 0.0), (11.0, 10.0), (7.5, 7.5)])
    func rpeIsClamped(wire: Double, expected: Double) {
        #expect(Rpe(wire: wire) == .rated(expected))
    }

    @Test("A non-finite RPE is unrated")
    func nonFiniteRpeIsUnrated() {
        #expect(Rpe(wire: .nan) == .unrated)
        #expect(Rpe(wire: .infinity) == .unrated)
    }

    @Test(
        "A TimeSpan reads as seconds in every spelling the server writes",
        arguments: [
            ("00:45:00", 2700.0),
            ("01:38:00", 5880.0),
            ("00:00:30", 30.0),
            ("1.02:00:00", 93_600.0),
            ("00:45:30.5000000", 2730.5),
            ("-00:30:00", -1800.0),
        ])
    func durationsParse(wire: String, expected: Double) {
        #expect(WireDuration.seconds(from: wire) == expected)
    }

    @Test("A TimeSpan that is not one is absent rather than zero")
    func anUnparseableDurationIsAbsent() {
        #expect(WireDuration.seconds(from: "soon") == nil)
        #expect(WireDuration.seconds(from: "45") == nil)
        #expect(WireDuration.seconds(from: "") == nil)
    }

    @Test("A distance-time set reads its duration as seconds")
    func aDistanceTimeSetCarriesItsDuration() throws {
        let json = """
            {"$type":"distanceTime","source":{"$type":"formed"},"facets":[],
             "movementDataId":"\(Session.movementId)","distance":5,"unit":3,
             "duration":"00:22:45","rpe":6}
            """

        guard case .distanceTime(let set) = try Decoding.decode(SetEntry.self, json) else {
            Issue.record("Expected .distanceTime")
            return
        }
        #expect(set.duration == 1365)
        #expect(set.unit == .kilometers)
    }

    @Test(
        "An instant parses with and without fractional seconds, and without an offset",
        arguments: [
            "2026-03-04T18:00:00+00:00",
            "2026-03-04T18:00:00.0000000+00:00",
            "2026-03-04T18:00:00Z",
            "2026-03-04T18:00:00",
        ])
    func instantsParse(wire: String) {
        // A DateTimeOffset serializes with a fractional part only when it has one, and a plain
        // DateTime arrives with no offset at all. All of them have to read or the first
        // whole-second timestamp of the day fails.
        #expect(WireDate.instant(from: wire) == Date(timeIntervalSince1970: 1_772_647_200))
    }

    @Test("A DateOnly is a calendar day with no time of day in it")
    func aDateOnlyStaysADay() {
        let day = CalendarDay(wire: "2026-03-04")

        #expect(day == CalendarDay(year: 2026, month: 3, day: 4))
        #expect(day?.wire == "2026-03-04")
    }

    @Test("A malformed DateOnly is absent rather than a guess")
    func aMalformedDayIsAbsent() {
        #expect(CalendarDay(wire: "March 4th") == nil)
        #expect(CalendarDay(wire: "2026-03") == nil)
        #expect(CalendarDay(wire: "") == nil)
    }

    @Test("Calendar days order by date")
    func daysOrderByDate() {
        #expect(CalendarDay(year: 2026, month: 3, day: 4) < CalendarDay(year: 2026, month: 3, day: 5))
        #expect(CalendarDay(year: 2025, month: 12, day: 31) < CalendarDay(year: 2026, month: 1, day: 1))
        #expect(CalendarDay(year: 2026, month: 2, day: 28) < CalendarDay(year: 2026, month: 3, day: 1))
    }

    @Test("An integer enum keeps the server's member order")
    func integerEnumsFollowTheServersOrder() {
        // These are persisted as ints, so the order is a storage contract on the server and a
        // decoding contract here. Getting one wrong restates pounds as kilograms.
        #expect(WeightUnit(wire: 0) == .kilograms)
        #expect(WeightUnit(wire: 1) == .pounds)
        #expect(DistanceUnit(wire: 0) == .meters)
        #expect(DistanceUnit(wire: 3) == .kilometers)
        #expect(Provenance(wire: 0) == .user)
        #expect(Provenance(wire: 1) == .aiAssisted)
        #expect(Valence(wire: 3) == .mixed)
        #expect(InjuryEvent.Kind(wire: 0) == .treatment)
        #expect(InjuryEvent.Kind(wire: 1) == .checkIn)
    }

    @Test("A texted utterance keeps the words and the parser's confidence in them")
    func atextedUtteranceKeepsItsWords() throws {
        let utterance = try Decoding.decode(
            Utterance.self,
            #"{"$type":"texted","rawText":"sled pull+push up to 235, 2 or 3 sets","structureConfidence":0.35}"#)

        #expect(utterance.rawText == "sled pull+push up to 235, 2 or 3 sets")
        #expect(utterance == .texted(
            rawText: "sled pull+push up to 235, 2 or 3 sets", structureConfidence: 0.35))
    }

    @Test("A formed utterance has no raw words, which is a different fact from empty ones")
    func aFormedUtteranceHasNoWords() throws {
        #expect(try Decoding.decode(Utterance.self, #"{"$type":"formed"}"#).rawText == nil)
    }
}

/// Oura and the other pre-remodel shapes.
@Suite("Pre-remodel wire shapes")
struct LegacyWireShapeTests {
    @Test("An Oura day reads the four panels the server sends")
    func anOuraDayReadsItsPanels() throws {
        let day = try Decoding.decode(OuraDay.self, Oura.day(date: "2026-03-04", readinessScore: 83))

        #expect(day.date == CalendarDay(year: 2026, month: 3, day: 4))
        #expect(day.readiness?.score == 83)
        #expect(day.readiness?.restingHeartRate == 48)
        #expect(day.sleep?.totalSleep == 27000)
        #expect(day.activity?.steps == 9000)
        #expect(day.resilience?.level == "solid")
    }

    @Test("An Oura day whose panels are null keeps the day and loses only the panels")
    func absentPanelsAreAbsentNotZero() throws {
        let day = try Decoding.decode(OuraDay.self, Oura.dayWithoutPanels(date: "2026-03-04"))

        #expect(day.date == CalendarDay(year: 2026, month: 3, day: 4))
        #expect(day.readiness == nil)
        #expect(day.sleep == nil)
    }

    @Test("A wellness state takes the server's own overall score rather than recomputing it")
    func wellnessTakesTheServersOverallScore() throws {
        // The server inverts stress before averaging. Recomputing it here is how two clients start
        // showing different numbers for the same day.
        let json = """
            {"stateID":"66666666-6666-6666-6666-666666666666","date":"2026-03-04",
             "motivationScore":4,"stressScore":2,"moodScore":5,"energyScore":4,"overallScore":4.25}
            """

        let state = try Decoding.decode(WellnessState.self, json)

        #expect(state.overallScore == 4.25)
        #expect(state.date == CalendarDay(year: 2026, month: 3, day: 4))
    }

    @Test("An injury carries its events")
    func anInjuryCarriesItsEvents() throws {
        let json = """
            {"injuryID":"77777777-7777-7777-7777-777777777777","name":"Left knee","notes":"patellar",
             "injuryDate":"2026-01-12","isActive":true,
             "injuryEvents":[
               {"injuryEventID":"88888888-8888-8888-8888-888888888888","trainingSessionID":null,
                "movementIDs":[],"notes":"aches on stairs","painLevel":3,"injuryType":1,
                "creationTime":"2026-02-20T08:00:00"}]}
            """

        let injury = try Decoding.decode(Injury.self, json)

        #expect(injury.name == "Left knee")
        #expect(injury.isActive)
        #expect(injury.events.count == 1)
        #expect(injury.events.first?.painLevel == 3)
        #expect(injury.events.first?.kind == .checkIn)
    }
}

/// The draft that goes back to `POST /confirm` unchanged.
@Suite("Interpretation resume")
struct InterpretationResumeTests {
    @Test("A committed outcome names what it wrote")
    func aCommittedOutcomeNamesWhatItWrote() throws {
        let json = """
            {"$type":"committed","sessionIds":["99999999-9999-9999-9999-999999999999"],
             "contextIds":[],"warnings":["Assumed 3 sets from \\"2 or 3\\""]}
            """

        guard case .committed(let committed) = try Decoding.decode(InterpretationOutcome.self, json)
        else {
            Issue.record("Expected .committed")
            return
        }
        #expect(committed.sessionIds.count == 1)
        #expect(committed.contextIds.isEmpty)
        #expect(committed.warnings.count == 1)
    }

    @Test("Needing a confirmation is a success that carries the names and the draft")
    func awaitingConfirmationIsASuccess() throws {
        let json = """
            {"$type":"awaitingConfirmation",
             "draft":{"entries":[{"$type":"session","notes":"","sets":[]}],"warnings":[]},
             "references":[{"rawName":"sled push","kind":0,
               "candidates":[{"entityId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","name":"Sled Push","confidence":0.82}]}]}
            """

        let outcome = try Decoding.decode(InterpretationOutcome.self, json)

        guard case .awaitingConfirmation(let awaiting) = outcome else {
            Issue.record("Expected .awaitingConfirmation")
            return
        }
        #expect(awaiting.references.first?.rawName == "sled push")
        #expect(awaiting.references.first?.kind == .movementBase)
        #expect(awaiting.references.first?.candidates.first?.name == "Sled Push")

        // And the state the screen shows puts it beside .committed, not beside .refused.
        guard case .needsConfirmation = QuickLogState.after(outcome) else {
            Issue.record("Needing a confirmation must not read as a failure")
            return
        }
    }

    @Test("A draft survives a decode and re-encode without losing a member")
    func aDraftRoundTripsWithoutLoss() throws {
        // Resuming hands the server back the draft it sent. Anything this client dropped on the way
        // through would change what the server re-resolves, silently.
        let json = """
            {"entries":[{"$type":"session","period":{"$type":"instant","at":"2026-03-04T18:00:00+00:00"},
              "notes":"sled","decoration":{"facets":[{"axis":0,"value":"strength"}],"freeTags":[],
              "valence":0,"summaryText":"sled work"},
              "sets":[{"$type":"lift","source":{"$type":"texted","rawText":"235x5","structureConfidence":0.9},
                "facets":[],"movement":{"baseName":"sled push","equipmentName":"sled","modifierName":""},
                "reps":5,"weight":235,"unit":1,"rpe":8.5}],
              "effort":{"$type":"none"}}],
             "warnings":["low confidence"]}
            """

        let draft = try Decoding.decode(JSONValue.self, json)
        let reEncoded = try JSONEncoder().encode(draft)
        let again = try Wire.decoder().decode(JSONValue.self, from: reEncoded)

        #expect(again == draft)

        // The members the server's re-resolve reads are still where it looks for them.
        guard case .object(let root) = again,
              case .array(let entries) = root["entries"],
              case .object(let entry) = entries.first,
              case .array(let sets) = entry["sets"],
              case .object(let set) = sets.first
        else {
            Issue.record("The draft's structure did not survive the round trip")
            return
        }
        #expect(entry["$type"] == .string("session"))
        #expect(set["reps"] == .number(5))
        #expect(set["rpe"] == .number(8.5))
        #expect(set["weight"] == .number(235))
    }

    @Test("An integral number is re-encoded without a decimal point")
    func integersStayIntegers() throws {
        // An int-typed member on the server will not bind from "5.0", so a round trip has to keep
        // 5 looking like 5.
        let encoded = try JSONEncoder().encode(JSONValue.object(["reps": .number(5)]))

        #expect(String(decoding: encoded, as: UTF8.self) == #"{"reps":5}"#)
    }

    @Test("Null, true and a string survive the round trip as themselves")
    func scalarsRoundTrip() throws {
        let json = #"{"a":null,"b":true,"c":"x","d":[1,2],"e":{"f":1.5}}"#

        let value = try Decoding.decode(JSONValue.self, json)
        let again = try Wire.decoder().decode(JSONValue.self, from: try JSONEncoder().encode(value))

        #expect(again == value)
        guard case .object(let object) = again else {
            Issue.record("Expected an object")
            return
        }
        #expect(object["a"] == .null)
        #expect(object["b"] == .bool(true))
        #expect(object["c"] == .string("x"))
        #expect(object["e"] == .object(["f": .number(1.5)]))
    }
}
