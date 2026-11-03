import Foundation
import Testing

@testable import Lionheart

/// The claim every wire type here makes: a value the server sends that this client has never heard
/// of produces a case, not a thrown error.
///
/// It is the property the whole decoding layer is built around, and nothing else in the toolchain
/// watches it — a `$type` the client does not know is exactly what a deployed server gains when the
/// backend adds a case, and the failure it causes without this is a screen that will not load at
/// all rather than one row that reads oddly.
@Suite("Unrecognised wire values degrade")
struct TotalDecodingTests {
    @Test("A lifecycle state the client does not know keeps the session's envelope")
    func unknownSessionKindKeepsItsEnvelope() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.ofUnknownKind())

        guard case .unrecognised(let type, let envelope) = session else {
            Issue.record("Expected .unrecognised, got \(session)")
            return
        }
        #expect(type == "abandoned")
        #expect(envelope.trainingSessionId == UUID(uuidString: Session.id))
        #expect(envelope.notes == "left early")
        #expect(envelope.date == CalendarDay(year: 2026, month: 3, day: 4))
    }

    @Test("A set shape the client does not know keeps the words it was logged with")
    func unknownSetShapeKeepsItsUtterance() throws {
        let json = """
            {"$type":"swimSet","source":{"$type":"texted","rawText":"1km easy","structureConfidence":0.4},
             "facets":[],"laps":20}
            """

        let entry = try Decoding.decode(SetEntry.self, json)

        guard case .unrecognised(let type, let common) = entry else {
            Issue.record("Expected .unrecognised, got \(entry)")
            return
        }
        #expect(type == "swimSet")
        #expect(common.source.rawText == "1km easy")
    }

    @Test("One unreadable row does not cost the rest of the window")
    func oneUnreadableRowDoesNotEmptyTheList() throws {
        // The middle row has no id, which is the one thing a session genuinely cannot do without:
        // there would be nothing to show and nothing to act on.
        let json = """
            [\(Session.completed(date: "2026-03-01")),
             {"$type":"completed","notes":"no id here","date":"2026-03-02"},
             \(Session.planned(date: "2026-03-06"))]
            """

        let sessions = try Decoding.list(TrainingSession.self, json)

        #expect(sessions.count == 2)
        #expect(sessions.map(\.date) == [
            CalendarDay(year: 2026, month: 3, day: 1),
            CalendarDay(year: 2026, month: 3, day: 6),
        ])
    }

    @Test("A set the client cannot read does not cost the session's other sets")
    func oneUnreadableSetDoesNotEmptyTheSession() throws {
        let sets = "[\(Session.liftSet(reps: 5)), 42, \(Session.liftSet(reps: 3))]"

        let session = try Decoding.decode(TrainingSession.self, Session.completed(sets: sets))

        #expect(session.sets.count == 2)
    }

    @Test(
        "An unrecognised discriminator on a nested hierarchy is a case, not a failure",
        arguments: [
            #"{"$type":"fortnight","start":"2026-03-01T00:00:00+00:00"}"#,
            #"{"$type":""}"#,
            "{}",
        ])
    func unknownPeriodKindIsACase(json: String) throws {
        let period = try Decoding.decode(Period.self, json)

        guard case .unrecognised = period else {
            Issue.record("Expected .unrecognised, got \(period)")
            return
        }
        #expect(period.startTime == nil)
    }

    @Test("An effort state the client does not know keeps its own case")
    func unknownEffortStateIsACase() throws {
        let effort = try Decoding.decode(EffortState.self, #"{"$type":"estimated","ratings":null}"#)

        #expect(effort == .unrecognised("estimated"))
        #expect(effort.ratings == nil)
    }

    @Test("A decoration state the client does not know keeps its own case")
    func unknownDecorationStateIsACase() throws {
        let decoration = try Decoding.decode(
            DecorationState.self, #"{"$type":"partiallyDecorated","facets":[]}"#)

        #expect(decoration == .unrecognised("partiallyDecorated"))
        #expect(decoration.facets.isEmpty)
    }

    @Test("An interpretation outcome the client does not know is reported, not mistaken for failure")
    func unknownInterpretationOutcomeIsReported() throws {
        let outcome = try Decoding.decode(
            InterpretationOutcome.self, #"{"$type":"queued","at":"2026-03-04T18:00:00Z"}"#)

        #expect(outcome == .unrecognised("queued"))

        // And the state the screen shows says the answer was not understood, rather than claiming
        // the entry failed to log — the server may well have written it.
        guard case .refused(.unavailable(let message)) = QuickLogState.after(outcome) else {
            Issue.record("Expected a refusal naming the unknown answer")
            return
        }
        #expect(message.contains("queued"))
    }

    @Test(
        "A facet axis outside the fixed set keeps its raw value",
        arguments: [(0, FacetAxis.modality), (5, .bodyState), (9, .unrecognised(9))])
    func unknownFacetAxisKeepsItsValue(wire: Int, expected: FacetAxis) throws {
        let facet = try Decoding.decode(Facet.self, #"{"axis":\#(wire),"value":"squat"}"#)

        #expect(facet.axis == expected)
        #expect(facet.value == "squat")
    }

    @Test("A unit outside the fixed set keeps its raw value rather than defaulting to one")
    func unknownUnitKeepsItsValue() {
        // Defaulting an unknown unit to kilograms would silently restate 235 lb as 235 kg, which is
        // worse than rendering no unit at all.
        #expect(WeightUnit(wire: 7) == .unrecognised(7))
        #expect(WeightUnit(wire: nil) == .unrecognised(-1))
        #expect(WeightUnit(wire: 7).abbreviation.isEmpty)
        #expect(DistanceUnit(wire: 42) == .unrecognised(42))
    }
}

/// What each known lifecycle state actually decodes to.
///
/// The claim is that every case of the hierarchy is reachable and distinguished: a decoder that
/// collapsed two states would still satisfy the degradation tests above.
@Suite("Every lifecycle state decodes to its own case")
struct SessionLifecycleDecodingTests {
    @Test("A completed session carries its sets, period, effort and decoration")
    func completedSessionCarriesItsActuals() throws {
        let json = Session.completed(
            sets: "[\(Session.liftSet())]",
            period: #"{"$type":"interval","start":"2026-03-04T17:00:00+00:00","end":"2026-03-04T18:12:00+00:00"}"#,
            effort: #"{"$type":"reported","ratings":{"recordedAt":"2026-03-04T18:15:00","difficultyRating":4,"accumulatedFatigue":null,"engagementRating":5,"externalVariablesRating":null}}"#,
            decoration: #"{"$type":"decorated","facets":[{"axis":0,"value":"strength"}],"freeTags":["sled"],"valence":0}"#,
            notes: "felt strong")

        guard case .completed(let session) = try Decoding.decode(TrainingSession.self, json) else {
            Issue.record("Expected .completed")
            return
        }

        #expect(session.sets.count == 1)
        #expect(session.period.duration == 4_320)   // 17:00 to 18:12
        #expect(session.effort.ratings?.difficultyRating == 4)
        #expect(session.effort.ratings?.accumulatedFatigue == nil)
        #expect(session.decoration == .decorated(
            facets: [Facet(axis: .modality, value: "strength")],
            freeTags: ["sled"],
            valence: .positive))
        #expect(session.envelope.notes == "felt strong")
    }

    @Test("A planned session carries its prescriptions and the day it is meant for")
    func plannedSessionCarriesItsIntentions() throws {
        let prescriptions = """
            [{"$type":"lift","movementDataId":"\(Session.movementId)","targetReps":5,
              "targetWeight":245,"unit":1,"targetRpe":null},
             {"$type":"freeform","text":"hard intervals, coach's choice"}]
            """
        let json = Session.planned(
            date: "2026-03-06", plannedFor: "2026-03-06", prescriptions: prescriptions)

        guard case .planned(let session) = try Decoding.decode(TrainingSession.self, json) else {
            Issue.record("Expected .planned")
            return
        }

        #expect(session.plannedFor == CalendarDay(year: 2026, month: 3, day: 6))
        #expect(session.prescriptions.count == 2)
        #expect(session.prescriptions.last == .freeform(text: "hard intervals, coach's choice"))

        guard case .lift(let lift) = session.prescriptions.first else {
            Issue.record("Expected a lift prescription")
            return
        }
        #expect(lift.targetReps == 5)
        #expect(lift.targetRpe == .unrated)
    }

    @Test("An active session carries when it started and what has been logged")
    func activeSessionCarriesItsProgress() throws {
        let json = Session.active(sets: "[\(Session.liftSet()), \(Session.liftSet(reps: 3))]")

        guard case .active(let session) = try Decoding.decode(TrainingSession.self, json) else {
            Issue.record("Expected .active")
            return
        }
        #expect(session.sets.count == 2)
        #expect(session.startedAt == Date(timeIntervalSince1970: 1_772_645_400))
    }

    @Test("A skipped session keeps the day it was planned for, because the skip is data")
    func skippedSessionKeepsItsPlannedDay() throws {
        guard case .skipped(let session) = try Decoding.decode(
            TrainingSession.self, Session.skipped(date: "2026-03-02"))
        else {
            Issue.record("Expected .skipped")
            return
        }
        #expect(session.plannedFor == CalendarDay(year: 2026, month: 3, day: 2))
    }

    @Test("A session with neither an id nor a date is the one row that cannot be read")
    func aSessionWithoutIdentityIsRefused() {
        #expect(throws: DecodingError.self) {
            try Decoding.decode(TrainingSession.self, #"{"$type":"completed","notes":"orphan"}"#)
        }
    }

    @Test("Each lifecycle state answers only its own case accessor")
    func theCaseAccessorsDistinguishTheStates() throws {
        let completed = try Decoding.decode(TrainingSession.self, Session.completed())
        let planned = try Decoding.decode(TrainingSession.self, Session.planned())
        let active = try Decoding.decode(TrainingSession.self, Session.active())
        let skipped = try Decoding.decode(TrainingSession.self, Session.skipped())

        #expect(completed.completedCase != nil)
        #expect(completed.plannedCase == nil)
        #expect(completed.activeCase == nil)

        #expect(planned.plannedCase != nil)
        #expect(planned.completedCase == nil)

        #expect(active.activeCase != nil)
        #expect(active.completedCase == nil)

        #expect(skipped.completedCase == nil)
        #expect(skipped.plannedCase == nil)
        #expect(skipped.activeCase == nil)
    }
}
