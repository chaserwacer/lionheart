import Foundation
import Observation

/// The athlete's body-composition scans.
///
/// App-wide because two screens read it: the Body tab's row, which leads with the latest scan, and
/// the composition screen. Every scan is read at once — they come weeks apart.
@Observable
@MainActor
final class BodyCompositionStore {
    private(set) var state: LoadState<CompositionHistory> = .loading
    /// Where a deletion asked for from a row stands. Recording and correcting throw instead, to the
    /// form that asked, which has to stay up holding what was typed while it says why.
    private(set) var action: ActionState = .idle

    private let api: BodyCompositionAPI
    private var load: Task<Void, Never>?

    init(api: BodyCompositionAPI) {
        self.api = api
    }

    var history: CompositionHistory { state.value ?? CompositionHistory([]) }

    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func refreshIfNeeded() async {
        guard state.value == nil || state.isStale else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        action = .idle
    }

    func dismissRefusal() { action = .idle }

    /// Records a scan, then re-reads.
    ///
    /// - Throws: `LoadFailure` when the server refuses or cannot be reached; `CancellationError`
    ///   when the form went away first.
    func record(_ scan: ScanSubmission) async throws {
        try await submit { _ = try await self.api.record(scan) }
    }

    /// Replaces a recorded scan with a corrected reading of its sheet, then re-reads.
    ///
    /// - Throws: as `record(_:)`.
    func correct(_ id: UUID, to scan: ScanSubmission) async throws {
        try await submit { _ = try await self.api.correct(id, to: scan) }
    }

    func delete(_ scan: BodyCompositionScan) async {
        guard !action.isRunning else { return }
        action = .running
        do {
            try await api.delete(scan.id)
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
            if action.revealsOutdatedScreen { await refresh() }
        }
    }

    private func submit(_ write: () async throws -> Void) async throws {
        do {
            try await write()
        } catch {
            throw try LoadFailure.from(error)
        }
        await refresh()
    }

    private func reload() async {
        do {
            let read = try await api.scans(remembering: .bodyComposition)
            state = state.applying(read.map(CompositionHistory.init))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
