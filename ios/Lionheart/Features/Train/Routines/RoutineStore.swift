import Foundation
import Observation

/// The athlete's routines, and the edits made to them.
///
/// Starting a workout from one is `TrainStore`'s, because it is a start like the other two. This
/// holds the list and turns each write the athlete asks for into one state case: the server's answer
/// replaces the routine in the list, and a refusal is stated rather than swallowed.
///
/// App-wide, because a finished workout's readout offers to save or update a routine after the
/// logging surface has gone, and Train shows the same list.
@Observable
@MainActor
final class RoutineStore {
    private(set) var state: LoadState<[Routine]> = .loading
    private(set) var action: ActionState = .idle

    private let routines: RoutinesAPI
    private var load: Task<Void, Never>?

    init(routines: RoutinesAPI) {
        self.routines = routines
    }

    var all: [Routine] { state.value ?? [] }

    func routine(_ id: UUID) -> Routine? { all.first { $0.routineId == id } }

    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        action = .idle
    }

    func dismissRefusal() { action = .idle }

    // MARK: - Writes

    /// A new routine, stated whole. Answers it, or `nil` when the server refused.
    @discardableResult
    func create(_ content: RoutineContent) async -> Routine? {
        await write { try await self.routines.create(content) }
    }

    /// A routine saved from a completed session, which becomes its first session.
    @discardableResult
    func create(named name: String, fromSession sessionId: UUID) async -> Routine? {
        let created = await write { try await self.routines.create(named: name, fromSession: sessionId) }
        // The session now names the routine, so the list's last-session answers are worth re-reading.
        if created != nil { await refresh() }
        return created
    }

    @discardableResult
    func replace(_ routineId: UUID, with content: RoutineContent) async -> Routine? {
        await write { try await self.routines.replace(routineId, with: content) }
    }

    func delete(_ routineId: UUID) async {
        guard !action.isRunning else { return }
        action = .running
        do {
            try await routines.delete(routineId)
            action = .idle
            state = state.editing { $0.filter { $0.routineId != routineId } }
        } catch {
            action = ActionState.refusal(from: error)
        }
    }

    /// Runs one write and puts the routine it answers into the list in place, or at the end.
    private func write(_ change: () async throws -> Routine) async -> Routine? {
        guard !action.isRunning else { return nil }
        action = .running
        do {
            let routine = try await change()
            action = .idle
            if state.value == nil {
                // Never loaded: the one routine is not the list, so read the list.
                await refresh()
            } else {
                state = state.editing { list in
                    list.contains { $0.routineId == routine.routineId }
                        ? list.map { $0.routineId == routine.routineId ? routine : $0 }
                        : list + [routine]
                }
            }
            return routine
        } catch {
            action = ActionState.refusal(from: error)
            return nil
        }
    }

    private func reload() async {
        do {
            state = state.applying(try await routines.routines(remembering: .trainRoutines))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
