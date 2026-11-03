import Foundation

/// The routine routes: the athlete's saved sessions, which a workout starts from.
///
/// A routine is replaced whole rather than edited a field at a time, like a session's set list:
/// it is one small document, and the client always holds all of it.
struct RoutinesAPI: Sendable {
    let client: APIClient

    private static let root = "/api/training/routines"

    /// Every routine, in the order they were made, each with its last completed session. Remembered,
    /// so a workout can start from one with no signal.
    func routines(remembering key: ReadKey) async throws -> Remembered<[Routine]> {
        try await client.get(Self.root, as: LenientList<Routine>.self, remembering: key).map(\.elements)
    }

    func create(_ content: RoutineContent) async throws -> Routine {
        try await client.post(Self.root, body: content)
    }

    /// Saves a completed session as a routine. The session becomes the routine's first, so a
    /// workout started from it is compared with that session.
    func create(named name: String, fromSession sessionId: UUID) async throws -> Routine {
        try await client.post(
            "\(Self.root)/from-session/\(sessionId.uuidString)", body: RoutineName(name: name))
    }

    func replace(_ routineId: UUID, with content: RoutineContent) async throws -> Routine {
        try await client.put("\(Self.root)/\(routineId.uuidString)", body: content)
    }

    func delete(_ routineId: UUID) async throws {
        try await client.delete("\(Self.root)/\(routineId.uuidString)")
    }
}

/// Everything about a routine the athlete states: what it is called, the notes, and its movements.
struct RoutineContent: Encodable, Sendable, Equatable {
    let name: String
    let notes: String
    let movements: [RoutineMovement]
}

private struct RoutineName: Encodable, Sendable {
    let name: String
}
