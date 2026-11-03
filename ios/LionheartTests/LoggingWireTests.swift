import Foundation
import Testing

@testable import Lionheart

/// The wire shapes the logging round added: set kinds, routines, last time on a movement,
/// bodyweight and a movement's setup — and the workout document that now carries a kind and a
/// routine.
@Suite("Logging wire")
struct LoggingWireTests {
    // MARK: - Set kind

    @Test("A lift the server sends without a kind is a working set")
    func aLiftWithoutAKindIsWorking() throws {
        let entry = try Decoding.decode(SetEntry.self, """
            {"$type":"lift","movementDataId":"\(Session.movementId)","reps":5,"weight":100,"unit":1,
             "rpe":null,"source":{"$type":"formed"},"facets":[]}
            """)

        #expect(entry.kind == .working)
    }

    @Test("Each kind the server sends decodes to its case")
    func kindsDecode() throws {
        let kinds = try [0, 1, 2, 3].map { wire in
            try Decoding.decode(SetEntry.self, """
                {"$type":"lift","movementDataId":"\(Session.movementId)","reps":5,"weight":100,
                 "unit":1,"rpe":null,"kind":\(wire),"source":{"$type":"formed"},"facets":[]}
                """).kind
        }

        #expect(kinds == [.working, .warmUp, .drop, .failure])
    }

    @Test("A kind this build does not know keeps its number, so a resumed set goes back as it came")
    func anUnknownKindRoundTrips() throws {
        let entry = try Decoding.decode(SetEntry.self, """
            {"$type":"lift","movementDataId":"\(Session.movementId)","reps":5,"weight":100,"unit":1,
             "rpe":null,"kind":9,"source":{"$type":"formed"},"facets":[]}
            """)

        #expect(entry.kind == .unrecognised(9))
        #expect(entry.kind.isWork)
        #expect(entry.command?.kind.wire == 9)
    }

    @Test("A warm-up is sent with its kind")
    func aWarmUpIsSentWithItsKind() throws {
        let warmUp = SetCommand.lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 10, weight: 45, unit: .pounds, rpe: .unrated,
            kind: .warmUp))

        let json = try JSONSerialization.jsonObject(with: Wire.encoder().encode(warmUp)) as? [String: Any]

        #expect(json?["kind"] as? Int == 1)
    }

    @Test("A set in a workout document written before kinds existed reads as a working set")
    func anOldDocumentsSetIsWorking() throws {
        let command = try Decoding.decode(SetCommand.self, """
            {"$type":"lift","movementDataId":"\(Session.movementId)","reps":5,"weight":100,"unit":1,"rpe":null}
            """)

        #expect(command.kind == .working)
    }

    // MARK: - The workout document

    @Test("A workout started from a routine keeps the routine through a write and a read")
    func aRoutineSurvivesTheDocument() throws {
        let routine = WorkoutRoutine(
            routineId: UUID(), name: "Upper",
            movements: [RoutineMovement(movementDataId: AWorkout.bench, workingSets: 2)])
        let workout = ActiveWorkout(
            destination: .deferredStart(localId: UUID()), startedAt: AWorkout.start,
            sets: [AWorkout.lift(kind: .warmUp)], addedMovementDataIds: [AWorkout.bench], routine: routine)

        let read = try Wire.decoder().decode(ActiveWorkout.self, from: Wire.encoder().encode(workout))

        #expect(read == workout)
    }

    @Test("A version 4 document, with no routine, still reads")
    func aVersionFourDocumentReads() throws {
        let read = try Decoding.decode(ActiveWorkout.self, """
            {"version":4,"destination":{"kind":"liveSession","sessionId":"\(Session.id)"},
             "startedAt":794000000,"sync":{"kind":"synced"},"sets":[],"rest":{"kind":"idle"},
             "prescriptions":[],"addedMovementDataIds":[],"notes":""}
            """)

        #expect(read.routine == nil)
        #expect(read.isReadable)
    }

    // MARK: - Routines and last time

    @Test("A routine decodes with its movements and the last session trained from it")
    func aRoutineDecodes() throws {
        let routine = try Decoding.decode(Routine.self, """
            {"routineId":"66666666-6666-6666-6666-666666666666","name":"Upper","notes":"pin 7",
             "movements":[{"movementDataId":"\(Session.movementId)","workingSets":2}],
             "createdAt":"2026-11-03T12:00:00Z","lastSession":\(Session.completed())}
            """)

        #expect(routine.name == "Upper")
        #expect(routine.movements == [RoutineMovement(
            movementDataId: UUID(uuidString: Session.movementId)!, workingSets: 2)])
        #expect(routine.lastSession?.envelope.date == CalendarDay(year: 2026, month: 3, day: 4))
        #expect(routine.workingSetCount == 2)
    }

    @Test("A routine never trained has no last session")
    func aFreshRoutineHasNoLastSession() throws {
        let routine = try Decoding.decode(Routine.self, """
            {"routineId":"66666666-6666-6666-6666-666666666666","name":"Lower","notes":"",
             "movements":[],"createdAt":"2026-11-03T19:06:37.714169","lastSession":null}
            """)

        #expect(routine.lastSession == nil)
    }

    @Test("A session names the routine it was trained from")
    func aSessionNamesItsRoutine() throws {
        let session = try Decoding.decode(TrainingSession.self, Session.completed()
            .replacingOccurrences(of: #""trainingProgramId":null"#,
                                  with: #""trainingProgramId":null,"routineId":"66666666-6666-6666-6666-666666666666""#))

        #expect(session.envelope.routineId == UUID(uuidString: "66666666-6666-6666-6666-666666666666"))
    }

    @Test("Last time on a movement reads its working sets by position, warm-ups set aside")
    func lastTimeReadsWorkingSetsByPosition() throws {
        let previous = try Decoding.decode(PreviousPerformance.self, """
            {"movementDataId":"\(Session.movementId)","trainingSessionId":"\(Session.id)",
             "date":"2026-09-16","startedAt":"2026-09-16T18:26:25-04:00","sets":[
              {"$type":"lift","movementDataId":"\(Session.movementId)","reps":10,"weight":0,"unit":1,"rpe":null,"kind":1,"source":{"$type":"formed"},"facets":[]},
              {"$type":"lift","movementDataId":"\(Session.movementId)","reps":3,"weight":70,"unit":1,"rpe":null,"kind":0,"source":{"$type":"formed"},"facets":[]},
              {"$type":"lift","movementDataId":"\(Session.movementId)","reps":6,"weight":45,"unit":1,"rpe":null,"kind":0,"source":{"$type":"formed"},"facets":[]}]}
            """)

        #expect(previous.sets.count == 3)
        guard case .lift(let first)? = previous.workingSet(at: 0),
              case .lift(let second)? = previous.workingSet(at: 1)
        else { Issue.record("expected two working lifts"); return }
        #expect((first.weight, first.reps) == (70, 3))
        #expect((second.weight, second.reps) == (45, 6))
        #expect(previous.workingSet(at: 2) == nil)
    }

    // MARK: - Bodyweight and setup

    @Test("A bodyweight decodes as the day, the weight and its unit")
    func aBodyweightDecodes() throws {
        let measurement = try Decoding.decode(
            BodyweightMeasurement.self, #"{"day":"2026-11-03","weight":175.5,"unit":1}"#)

        #expect(measurement == BodyweightMeasurement(
            day: CalendarDay(year: 2026, month: 11, day: 3), weight: 175.5, unit: .pounds))
    }

    @Test("A movement carries the athlete's setup, and one without it reads as none")
    func aMovementCarriesItsSetup() throws {
        let movements = try Decoding.list(MovementData.self, """
            [{"movementDataID":"\(Session.movementId)","movementBase":{"name":"Dip"},
              "equipment":{"name":"Dip Bars"},"setup":{"notes":"belt, 2 plates","addsToBodyweight":true}},
             {"movementDataID":"\(Session.id)","movementBase":{"name":"Bench Press"},
              "equipment":{"name":"Barbell"}}]
            """)

        #expect(movements[0].setup == MovementSetup(notes: "belt, 2 plates", addsToBodyweight: true))
        #expect(movements[1].setup == .none)
        #expect(movements[1].isBarbell)
        #expect(!movements[0].isBarbell)
    }
}
