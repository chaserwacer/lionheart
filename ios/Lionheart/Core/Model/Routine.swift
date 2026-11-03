import Foundation

/// A session the athlete trains again and again — "Upper", "Lower", "Full body" — saved so a
/// workout can start from it in a tap.
///
/// Its content is an order of movements and how many working sets each gets, and nothing about
/// weights or reps. The athlete takes two sets to failure and progresses by beating last time, so
/// the numbers a workout opens with come from the previous performance of each movement, not from
/// a snapshot frozen into the routine the day it was saved.
struct Routine: Equatable, Sendable, Identifiable {
    let routineId: UUID
    let name: String
    let notes: String
    let movements: [RoutineMovement]
    let createdAt: Date?
    /// The most recent completed session trained from this routine, which is what a finished
    /// workout is compared against. Absent until one has been.
    let lastSession: TrainingSession.Completed?

    var id: UUID { routineId }

    /// How many working sets the routine asks for in all.
    var workingSetCount: Int { movements.reduce(0) { $0 + $1.workingSets } }
}

/// One movement of a routine, and how many working sets it gets.
struct RoutineMovement: Equatable, Sendable, Codable, Identifiable {
    let movementDataId: UUID
    let workingSets: Int

    var id: UUID { movementDataId }

    /// The bounds the server holds a routine to.
    static let setRange = 1...20
}

extension Routine: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let routineId = container.lenient(UUID.self, "routineId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A routine needs a routineId."))
        }
        self.routineId = routineId
        name = container.lenient("name", or: "")
        notes = container.lenient("notes", or: "")
        movements = container.lenientList(RoutineMovement.self, "movements")
        createdAt = container.instant("createdAt")
        lastSession = container.lenient(TrainingSession.self, "lastSession")?.completedSession
    }
}

extension TrainingSession {
    var completedSession: Completed? {
        switch self {
        case .completed(let session): session
        case .planned, .active, .skipped, .unrecognised: nil
        }
    }
}

/// The routine a workout in flight was started from, as the workout's own document holds it.
///
/// Copied into the document rather than looked up, for the reason the prescriptions of a planned
/// session are: the plan has to be on screen in a basement, and the routine may be renamed or
/// deleted on another device while the workout runs.
struct WorkoutRoutine: Equatable, Sendable, Codable {
    let routineId: UUID
    let name: String
    let movements: [RoutineMovement]

    init(routineId: UUID, name: String, movements: [RoutineMovement]) {
        self.routineId = routineId
        self.name = name
        self.movements = movements
    }

    init(_ routine: Routine) {
        self.init(routineId: routine.routineId, name: routine.name, movements: routine.movements)
    }

    /// How many working sets the routine asks of a movement, or `nil` where it names none.
    func workingSets(of movementDataId: UUID) -> Int? {
        movements.first { $0.movementDataId == movementDataId }?.workingSets
    }
}
