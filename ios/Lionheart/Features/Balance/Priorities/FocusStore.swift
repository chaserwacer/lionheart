import Foundation
import Observation

/// The focus pins — each family's priority lift and the movements emphasised on their own — and the
/// four things the athlete can do to one.
///
/// It holds the pins and nothing derived from them. How a pin is going is a reading of the log
/// (`FocusReading`), which `HistoryStore` already holds whole, so the screen reads the two stores
/// together rather than this one copying the log.
@Observable
@MainActor
final class FocusStore {
    private(set) var state: LoadState<[FocusPin]> = .loading

    /// Where an unpin or a delete asked for from a row stands. Pinning and revising answer their
    /// sheet directly instead, since a refusal has to be stated where the athlete is typing.
    private(set) var action: ActionState = .idle

    private let api: FocusAPI
    private let now: @Sendable () -> Date
    private var load: Task<Void, Never>?

    init(api: FocusAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.api = api
        self.now = now
    }

    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func refreshIfNeeded() async {
        guard state.value == nil else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        action = .idle
    }

    /// The pins on one movement, pinned or not.
    func pins(on movementDataId: UUID) -> [FocusPin] {
        (state.value ?? []).filter { $0.movementDataId == movementDataId }
    }

    // MARK: - Writes

    /// Pins a movement — as a family's priority when one is named. Answers the refusal, if there
    /// was one, for the sheet to state.
    func pin(_ movementDataId: UUID, _ draft: FocusDraft, priority: PatternFamily? = nil) async -> LoadFailure? {
        await answering { _ = try await self.api.pin(movementDataId, draft, priority: priority) }
    }

    /// The pin standing as the family's priority today, if any.
    func priority(for family: PatternFamily) -> FocusPin? {
        (state.value ?? []).first { $0.isPinned && $0.role.priorityFamily == family }
    }

    func revise(_ pin: FocusPin, to draft: FocusDraft) async -> LoadFailure? {
        await answering { _ = try await self.api.revise(pin.focusPinId, to: draft) }
    }

    /// Takes a pin off the list today. Kept on the server, as a past focus.
    func unpin(_ pin: FocusPin) async {
        await write { _ = try await self.api.unpin(pin.focusPinId, on: CalendarDay(self.now())) }
    }

    /// The same, asked from the pin's sheet, which states a refusal itself.
    func unpinFromSheet(_ pin: FocusPin) async -> LoadFailure? {
        await answering { _ = try await self.api.unpin(pin.focusPinId, on: CalendarDay(self.now())) }
    }

    /// Removes a pin made by mistake.
    func delete(_ pin: FocusPin) async {
        await write { try await self.api.delete(pin.focusPinId) }
    }

    func dismissRefusal() {
        action = .idle
    }

    /// A sheet's write, then a re-read. The pins are re-read rather than the answer spliced in, so
    /// the list's order stays the server's.
    private func answering(_ change: () async throws -> Void) async -> LoadFailure? {
        do {
            try await change()
            await refresh()
            return nil
        } catch {
            return ActionState.refusal(from: error).refusal
        }
    }

    /// A row's write, with its refusal kept and stated. A 409 — unpinned elsewhere already — means
    /// the list is out of date, so it is read again under the alert.
    private func write(_ change: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running
        do {
            try await change()
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
            if action.revealsOutdatedScreen { await refresh() }
        }
    }

    private func reload() async {
        do {
            let read = try await api.pins(remembering: .focusPins)
            state = state.applying(read)
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
