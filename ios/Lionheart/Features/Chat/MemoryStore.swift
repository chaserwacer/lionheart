import Foundation
import Observation

/// What Lionheart remembers from the thread, and the two things the athlete can do to a memory.
@Observable
@MainActor
final class MemoryStore {
    private(set) var state: LoadState<[ChatMemory]> = .loading

    /// Where a delete asked for from a row stands. A correction answers its editor directly, since a
    /// refusal has to be stated where the athlete is typing.
    private(set) var action: ActionState = .idle

    private let api: ChatMemoryAPI
    private var load: Task<Void, Never>?

    init(api: ChatMemoryAPI) {
        self.api = api
    }

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

    /// Replaces what a memory says. Answers with the new version, which has an id of its own, or
    /// with the refusal for the editor to state.
    ///
    /// A `Result` because the failure is handed to the editor to hold and show, not propagated.
    func correct(_ memory: ChatMemory, kind: MemoryKind, text: String) async -> Result<ChatMemory, LoadFailure> {
        do {
            let corrected = try await api.correct(memory.memoryId, kind: kind, text: text)
            await refresh()
            return .success(corrected)
        } catch {
            let refusal = ActionState.refusal(from: error)
            if refusal.revealsOutdatedScreen { await refresh() }
            return .failure(refusal.refusal ?? .unavailable("The correction was cancelled."))
        }
    }

    /// Forgets a memory, every version of it.
    func delete(_ memory: ChatMemory) async {
        guard !action.isRunning else { return }
        action = .running
        do {
            try await api.delete(memory.memoryId)
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
            if action.revealsOutdatedScreen { await refresh() }
        }
    }

    func dismissRefusal() {
        action = .idle
    }

    private func reload() async {
        do {
            state = state.applying(try await api.memories(remembering: .chatMemories))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
