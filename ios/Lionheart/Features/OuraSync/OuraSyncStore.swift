import Foundation
import Observation

/// Bringing the stored Oura data up to date, on request and when the app comes back to the
/// foreground behind.
///
/// One store for the whole app rather than one per tab, because Today and Body both show the
/// control and a sync started from one must be the sync the other shows — two stores would let
/// both start one.
///
/// What it plans and when it starts one on its own are pure functions in `OuraSyncPlan`; this type
/// does the three requests and turns their outcome into one state.
@Observable
@MainActor
final class OuraSyncStore {
    private(set) var state: OuraSyncState = .unknown

    private let oura: any OuraSyncing
    private let now: @Sendable () -> Date

    /// When a sync was last started, by hand or automatically — what the automatic one waits on.
    private var lastAttempt: Date?
    private var running: Task<Void, Never>?
    private var whenSynced: [@MainActor () async -> Void] = []
    /// Moves on at every `reset()`. A read that was out across a sign-out compares it before
    /// writing, so one athlete's coverage cannot land in the state the next sign-in starts from.
    private var generation = 0

    init(oura: any OuraSyncing, now: @escaping @Sendable () -> Date = { Date() }) {
        self.oura = oura
        self.now = now
    }

    /// Registers what to re-read once a sync has stored something, so the screens showing Oura data
    /// catch up without this store knowing which screens exist.
    func onSynced(_ action: @escaping @MainActor () async -> Void) {
        whenSynced.append(action)
    }

    /// Reads how far the stored data reaches, without syncing. A failure leaves the state as it
    /// was: the control can still offer a sync, and the sync reads the coverage again first.
    func refreshCoverage() async {
        guard !state.isSyncing else { return }
        let session = generation
        do {
            let coverage = try await oura.coverage()
            guard session == generation, !state.isSyncing else { return }
            state = .idle(coverage)
        } catch {
            return
        }
    }

    /// Syncs because the athlete asked. Joins a sync already out rather than starting a second.
    func sync() async {
        if let running {
            await running.value
            return
        }
        let session = generation
        let task = Task { await self.run() }
        running = task
        await task.value
        if session == generation { running = nil }
    }

    /// Syncs because the app came back to the foreground, if the data is behind and the last
    /// attempt was long enough ago.
    func syncIfBehind() async {
        // Read first, so a cold launch onto data that is already current does not ask Oura for
        // today on the strength of not knowing.
        if state.coverage == nil { await refreshCoverage() }
        let moment = now()
        guard OuraSyncPlan.warrantsAutomaticSync(
            state, lastAttempt: lastAttempt, now: moment, today: CalendarDay(moment))
        else { return }
        await sync()
    }

    func reset() {
        generation += 1
        running?.cancel()
        running = nil
        lastAttempt = nil
        state = .unknown
    }

    /// Read the coverage, plan the range, sync it, read the coverage again — one railway, ended in
    /// one `do`/`catch`. The second read is what moves the control's "through" day; the report
    /// alone cannot say it, since Oura may have had nothing newer.
    ///
    /// Only `reset()` cancels a run, which is a sign-out: every step checks for it after its
    /// await, and a cancelled run writes nothing.
    private func run() async {
        let moment = now()
        lastAttempt = moment
        let known = state.coverage

        do {
            let coverage = try await oura.coverage()
            try Task.checkCancellation()
            guard let range = OuraSyncPlan.range(for: coverage, today: CalendarDay(moment)) else {
                state = .idle(coverage)
                return
            }
            state = .syncing(range, from: coverage)
            let report = try await oura.sync(range)
            try Task.checkCancellation()
            let after = try await oura.coverage()
            try Task.checkCancellation()
            state = .synced(report, coverage: after)
            for action in whenSynced { await action() }
        } catch is CancellationError {
            return
        } catch {
            guard !Task.isCancelled else { return }
            let failure = (try? OuraSyncFailure.from(error)) ?? .unavailable(String(describing: error))
            state = .failed(failure, coverage: state.coverage ?? known)
        }
    }
}
