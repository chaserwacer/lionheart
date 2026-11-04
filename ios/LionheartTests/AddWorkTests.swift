import Foundation
import Testing

@testable import Lionheart

/// The Add sheet's lists, the cardio it opens, and the taps the entry row saves.
@Suite("Adding and logging work")
struct AddWorkTests {
    static let row = UUID(uuidString: "55555555-5555-5555-5555-555555555555")!

    static func catalogue() throws -> [MovementData] {
        try Decoding.list(MovementData.self, """
            [\(Catalogue.movement(id: AWorkout.bench.uuidString, base: "Bench Press")),
             \(Catalogue.movement(id: AWorkout.squat.uuidString, base: "Squat")),
             \(Catalogue.movement(id: row.uuidString, base: "Row", equipment: "Rower"))]
            """)
    }

    /// Last time on the rower was distance and time, which is what makes it cardio.
    static let previous: [UUID: PreviousPerformance] = [
        row: PreviousPerformance(
            movementDataId: row, trainingSessionId: UUID(), date: CalendarDay(year: 2026, month: 3, day: 1),
            startedAt: nil,
            sets: [.distanceTime(SetEntry.DistanceTime(
                common: SetEntry.Common(source: .formed, facets: []), movementDataId: row, distance: 2000,
                unit: .meters, duration: 480, rpe: .unrated))]),
    ]

    @Test("Priorities lead, recent leaves them out, and a movement last logged as distance is cardio")
    func theSheetsSections() throws {
        let menu = AddWorkMenu.of(
            catalogue: try Self.catalogue(), recentIds: [AWorkout.squat, AWorkout.bench, AWorkout.squat],
            priorityIds: [AWorkout.bench], previous: Self.previous, query: "")

        #expect(menu.priorities.map(\.movementDataId) == [AWorkout.bench])
        #expect(menu.recent.map(\.movementDataId) == [AWorkout.squat])
        #expect(menu.distanceMovements.map(\.movementDataId) == [Self.row])
        #expect(menu.cardio == CardioWork.offered)
        #expect(menu.movements.count == 3)
    }

    @Test("A query is a search of movements and cardio alike, and nothing else")
    func aQueryIsASearch() throws {
        let menu = AddWorkMenu.of(
            catalogue: try Self.catalogue(), recentIds: [AWorkout.bench], priorityIds: [AWorkout.bench],
            previous: Self.previous, query: "ride")

        #expect(menu.priorities.isEmpty)
        #expect(menu.recent.isEmpty)
        #expect(menu.cardio == [.activity(.ride), .activity(.indoorRide)])
        #expect(menu.movements.isEmpty)
        #expect(!menu.isEmpty)
    }

    @Test("Cardio opens on the last of the same work in this workout")
    func cardioRepeatsTheLastOfItsKind() {
        let ride = LoggedSet(loggedAt: AWorkout.start, command: .endurance(SetCommand.Endurance(
            activity: .ride, distance: 20, unit: .miles, duration: 3600, elevationGainMeters: 120, rpe: .unrated)))
        let workout = AWorkout.live(sets: [ride])

        #expect(CardioWork.activity(.ride).nextSet(in: workout) == ride.command)
    }

    @Test("Cardio new to the workout opens empty, in the unit the last distance was logged in")
    func newCardioTakesTheLastDistanceUnit() {
        let workout = AWorkout.live(sets: [AWorkout.run(unit: .miles)])

        guard case .trailRun(let run) = CardioWork.outdoorRun.nextSet(in: workout) else {
            Issue.record("An outdoor run is a trail run set")
            return
        }
        #expect(run.distance == 0)
        #expect(run.unit == .miles)
        guard case .endurance(let hike) = CardioWork.activity(.hike).nextSet(in: AWorkout.live()) else {
            Issue.record("A hike is an endurance set")
            return
        }
        #expect(hike.activity == .hike)
        #expect(hike.unit == SetDefaults.distanceUnit)
    }

    @Test("A weight step is a plate pair in the unit, and steps never go below nothing or one rep")
    func stepping() {
        var draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 1, weight: 2.5, unit: .pounds, rpe: .unrated)))

        #expect(draft.steppingWeight(by: 1).weight == "7.5")
        #expect(draft.steppingWeight(by: -1).weight == "0")
        #expect(draft.steppingReps(by: -1).reps == "1")
        draft.weightUnit = .kilograms
        #expect(draft.steppingWeight(by: 2).weight == "7.5")
        draft.weight = ""
        #expect(draft.steppingWeight(by: 1).weight == "2.5")
    }

    @Test("Marking a set a drop takes a fifth off, down to a step; marking it again does not")
    func markingADrop() {
        let draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: AWorkout.bench, reps: 5, weight: 320, unit: .pounds, rpe: .unrated)))

        let dropped = draft.marked(.drop)
        #expect(dropped.kind == .drop)
        #expect(dropped.weight == "255")
        #expect(dropped.marked(.drop).weight == "255")
        #expect(draft.marked(.failure).weight == "320")
    }

    @Test("Warm-ups are not sets on the bar or the Lock Screen, as they are not in the totals")
    func warmUpsAreNotCounted() {
        let workout = AWorkout.live(sets: [
            AWorkout.lift(weight: 60, kind: .warmUp), AWorkout.lift(), AWorkout.lift(kind: .drop),
        ])

        #expect(workout.workSetCount == 2)
        #expect(workout.workSetCount == WorkoutTotals.of(workout.sets).setCount)
        let projection = WorkoutActivityProjection.of(workout, movementTitle: nil, isPersisted: true, at: AWorkout.start)
        #expect(projection?.state.setCount == 2)
    }
}
