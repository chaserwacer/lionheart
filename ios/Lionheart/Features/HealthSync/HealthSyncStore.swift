import Foundation
import Observation

/// Bringing the workouts other apps wrote to Apple Health into Lionheart, on request and when the app
/// comes back to the foreground.
///
/// One store for the whole app, because Today and Settings both show the row and pull to refresh on
/// History and Cardio starts a sync: two stores would let two syncs offer the same workouts at once.
///
/// A sync reads Health's workouts after the kept anchor a page at a time and offers each to the server,
/// which answers what became of it. The anchor moves on only once a whole page has been answered,
/// so a sync that stops — the server out of reach, Health refusing a read — starts the next one at
/// the page it stopped in; the server's import is idempotent, so a run offered twice is answered
/// "already imported" and written once. A run the server turns down is held back and offered again
/// first on every sync after (`HealthSyncMark`). When it is due on its own is a pure function in
/// `HealthSyncPlan`.
@Observable
@MainActor
final class HealthSyncStore {
    private(set) var state: HealthSyncState = .unknown

    private let health: any HealthWorkoutSource
    private let server: any HealthImporting
    private let marks: any HealthSyncMarks
    private let now: @Sendable () -> Date

    /// When a sync was last started, by hand or automatically — what the automatic one waits on.
    private var lastAttempt: Date?
    private var running: Task<Void, Never>?
    private var whenImported: [@MainActor () async -> Void] = []
    /// Moves on at every `reset()`. A read that was out across a sign-out compares it before
    /// writing, so one athlete's sync cannot land in the state the next sign-in starts from.
    private var generation = 0

    init(
        health: any HealthWorkoutSource,
        server: any HealthImporting,
        marks: any HealthSyncMarks,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.health = health
        self.server = server
        self.marks = marks
        self.now = now
    }

    /// Registers what to re-read once a sync has imported a run, so the screens showing training
    /// catch up without this store knowing which screens exist.
    func onImported(_ action: @escaping @MainActor () async -> Void) {
        whenImported.append(action)
    }

    /// Reads where Health stands on this device — whether it is here, whether the athlete has been
    /// asked — and when the last sync finished, without syncing.
    func refreshStatus() async {
        guard !state.isSyncing else { return }
        guard health.isAvailable else {
            state = .unavailable
            return
        }
        let session = generation
        let needsAsking = await health.needsAuthorization()
        guard session == generation, !state.isSyncing else { return }
        state = needsAsking ? .notConnected : .idle(lastSynced: marks.read().lastSynced)
    }

    /// Shows Health's own sheet for what Lionheart reads, then syncs. The athlete's choice is
    /// Health's to keep: a refusal reads as no workouts, which Health does not let an app tell apart.
    func connect() async {
        do {
            try await health.requestAuthorization()
        } catch is CancellationError {
            return
        } catch {
            state = .failed(.healthUnreadable(error.localizedDescription), done: .empty, lastSynced: marks.read().lastSynced)
            return
        }
        await sync()
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

    /// Syncs because the app came back to the foreground, if Health is connected and the last
    /// attempt was long enough ago.
    func syncIfDue() async {
        if case .unknown = state { await refreshStatus() }
        guard HealthSyncPlan.warrantsAutomaticSync(state, lastAttempt: lastAttempt, now: now()) else { return }
        await sync()
    }

    /// Syncs because the athlete pulled to refresh a screen that shows training — only once Health
    /// is connected, since asking for access is a decision made from the row.
    func syncIfConnected() async {
        if case .unknown = state { await refreshStatus() }
        guard state.isConnected else { return }
        await sync()
    }

    /// Forgets the kept anchor with everything else at sign-out: the next athlete's server has none
    /// of these workouts, so their first sync offers Health's whole history again.
    func reset() {
        generation += 1
        running?.cancel()
        running = nil
        lastAttempt = nil
        marks.clear()
        state = .unknown
    }

    // MARK: - The sync

    /// How far a sync has come: the mark to resume from, what it has answered so far, and the
    /// workouts the server turned down this time.
    private struct Walk {
        let mark: HealthSyncMark
        let tally: HealthSyncTally
        let heldBack: [UUID]
        let isFinished: Bool
    }

    /// The workouts held back last time first, then page after page until Health has nothing more,
    /// the mark kept after each; then the finish time is kept and the screens that show training are
    /// read again.
    ///
    /// Only `reset()` cancels a run, which is a sign-out: every step checks for it after its await,
    /// and a cancelled run writes nothing.
    private func run() async {
        guard health.isAvailable else {
            state = .unavailable
            return
        }
        lastAttempt = now()
        let kept = marks.read()
        var walk = Walk(mark: kept, tally: .empty, heldBack: [], isFinished: false)
        state = .syncing(progress: walk.tally, lastSynced: kept.lastSynced)

        do {
            walk = try await retried(walk)
            while !walk.isFinished {
                walk = try await synced(nextPageOf: walk)
            }
            let finishedAt = now()
            marks.write(HealthSyncMark(anchor: walk.mark.anchor, lastSynced: finishedAt, heldBack: walk.heldBack))
            state = .synced(walk.tally, at: finishedAt)
        } catch is CancellationError {
            return
        } catch {
            guard !Task.isCancelled else { return }
            let failure = (try? HealthSyncFailure.from(error)) ?? .unavailable(String(describing: error))
            state = .failed(failure, done: progress ?? walk.tally, lastSynced: kept.lastSynced)
        }

        // A sync that stopped partway may still have imported workouts, and those are on the server.
        if (state.done ?? .empty).imported > 0 {
            for action in whenImported { await action() }
        }
    }

    /// What the sync in flight has answered so far, including the page it is part-way through.
    private var progress: HealthSyncTally? {
        if case .syncing(let progress, _) = state { progress } else { nil }
    }

    /// The workouts the server turned down last time, offered again. Those it turns down again stay
    /// held back; one Health no longer holds is let go.
    private func retried(_ walk: Walk) async throws -> Walk {
        guard !walk.mark.heldBack.isEmpty else { return walk }
        let (tally, heldBack) = try await answered(try await health.workouts(walk.mark.heldBack), after: walk)
        let mark = HealthSyncMark(anchor: walk.mark.anchor, lastSynced: walk.mark.lastSynced, heldBack: heldBack)
        marks.write(mark)
        return Walk(mark: mark, tally: tally, heldBack: heldBack, isFinished: false)
    }

    /// One page read from Health and every workout on it answered, then the mark kept: the page's
    /// anchor, and any workout on it the server turned down, held back.
    private func synced(nextPageOf walk: Walk) async throws -> Walk {
        let page = try await health.page(after: walk.mark.anchor)
        try Task.checkCancellation()

        let (tally, heldBack) = try await answered(page.workouts, after: walk)
        let mark = HealthSyncMark(anchor: page.anchor, lastSynced: walk.mark.lastSynced, heldBack: heldBack)
        marks.write(mark)
        return Walk(mark: mark, tally: tally, heldBack: heldBack, isFinished: page.isLast)
    }

    /// Each workout offered in turn and its reply counted, the progress shown as it goes; what the
    /// server turned down is added to what the walk already holds back.
    private func answered(
        _ workouts: [HealthWorkoutImport], after walk: Walk
    ) async throws -> (tally: HealthSyncTally, heldBack: [UUID]) {
        var tally = walk.tally
        var heldBack = walk.heldBack
        for workout in workouts {
            let reply = try await server.offer(workout)
            try Task.checkCancellation()
            tally = tally.adding(reply)
            if case .refused = reply { heldBack.append(workout.workoutId) }
            state = .syncing(progress: tally, lastSynced: walk.mark.lastSynced)
        }
        return (tally, heldBack)
    }
}
