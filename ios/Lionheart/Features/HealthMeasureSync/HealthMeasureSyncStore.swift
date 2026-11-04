import Foundation
import Observation

/// Bringing what Apple's devices measured into Lionheart: the watch's fitness and recovery figures,
/// the phone's gait, the weigh-ins and the date of birth.
///
/// One store for the app, beside the workout sync rather than inside it: the two read different
/// things on different clocks (an anchor of workouts against a re-read week of days), and share only
/// Health's permission sheet, which `HealthLibrary` asks for everything at once.
///
/// A sync offers the weigh-ins of everything it is about to read in one request, then reads Health
/// a quarter at a time from a week before the last day it finished (`HealthMeasureSyncPlan`),
/// offering each quarter's days and moving the mark on after each, so one stopped partway starts
/// the next where it stopped. The server restates a day offered twice, so the overlap costs
/// nothing. The weigh-ins go in one request because the server reports how many weighted sets
/// read a different bodyweight against what it held before; quarter by quarter, every quarter's
/// weigh-ins would re-read the later sets again and the count would add them up many times.
@Observable
@MainActor
final class HealthMeasureSyncStore {
    private(set) var state: HealthMeasureSyncState = .unknown

    /// Days per request: a quarter of every figure is about a thousand, which one request carries.
    static let daysPerOffer = 1_000

    private let health: any HealthMeasureSource
    private let server: any HealthMeasureImporting
    private let marks: any HealthMeasureMarks
    private let now: @Sendable () -> Date
    private let calendar: Calendar

    private var lastAttempt: Date?
    private var running: Task<Void, Never>?
    private var whenSynced: [@MainActor (HealthMeasureTally) async -> Void] = []
    /// Moves on at every `reset()`, so a sync out across a sign-out cannot write into the next session.
    private var generation = 0

    init(
        health: any HealthMeasureSource,
        server: any HealthMeasureImporting,
        marks: any HealthMeasureMarks,
        now: @escaping @Sendable () -> Date = { Date() },
        calendar: Calendar = .current
    ) {
        self.health = health
        self.server = server
        self.marks = marks
        self.now = now
        self.calendar = calendar
    }

    /// Registers what to re-read once a sync has changed anything on the server.
    func onSynced(_ action: @escaping @MainActor (HealthMeasureTally) async -> Void) {
        whenSynced.append(action)
    }

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

    /// Shows Health's sheet for everything Lionheart reads, then syncs.
    func connect() async {
        do {
            try await health.requestAuthorization()
        } catch is CancellationError {
            return
        } catch {
            state = .failed(error.localizedDescription, done: .empty, lastSynced: marks.read().lastSynced)
            return
        }
        await sync()
    }

    /// Syncs because the athlete asked; joins a sync already out.
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

    /// Syncs on returning to the app, once Health is connected and the last attempt was long enough ago.
    ///
    /// Asks Health again while not connected: the workout sync's row asks for the same sheet, and
    /// connecting there connects this too.
    func syncIfDue() async {
        switch state {
        case .unknown, .notConnected: await refreshStatus()
        case .unavailable, .idle, .syncing, .synced, .failed: break
        }
        guard HealthMeasureSyncPlan.warrantsAutomaticSync(state, lastAttempt: lastAttempt, now: now()) else { return }
        await sync()
    }

    /// Forgets the mark at sign-out: the next athlete's server holds none of these days.
    func reset() {
        generation += 1
        running?.cancel()
        running = nil
        lastAttempt = nil
        marks.clear()
        state = .unknown
    }

    // MARK: - The sync

    private func run() async {
        guard health.isAvailable else {
            state = .unavailable
            return
        }
        guard !(await health.needsAuthorization()) else {
            state = .notConnected
            return
        }
        lastAttempt = now()
        let kept = marks.read()
        var tally = HealthMeasureTally.empty
        state = .syncing(tally, through: nil, lastSynced: kept.lastSynced)

        do {
            let earliest = kept.syncedThroughDay == nil
                ? try await health.earliestReading().map { CalendarDay($0, in: calendar) }
                : nil
            let windows = HealthMeasureSyncPlan.windows(earliest: earliest, mark: kept, today: CalendarDay(now(), in: calendar))
            tally = try await weighedIn(across: windows, after: tally)
            for window in windows {
                tally = try await synced(window, after: tally)
                try Task.checkCancellation()
                marks.write(HealthMeasureMark(syncedThrough: window.to, lastSynced: kept.lastSynced))
                state = .syncing(tally, through: window.to, lastSynced: kept.lastSynced)
            }
            try await applyDateOfBirth()
            try Task.checkCancellation()
            let finishedAt = now()
            marks.write(HealthMeasureMark(syncedThrough: windows.last?.to ?? kept.syncedThroughDay, lastSynced: finishedAt))
            state = .synced(tally, at: finishedAt)
        } catch is CancellationError {
            return
        } catch {
            guard !Task.isCancelled else { return }
            let message = (try? LoadFailure.from(error))?.message ?? String(describing: error)
            state = .failed(message, done: tally, lastSynced: kept.lastSynced)
        }

        if tally.changedAnything {
            for action in whenSynced { await action(tally) }
        }
    }

    /// Every weigh-in from the first window's start through the last's end, offered in one request.
    private func weighedIn(across windows: [DayRange], after tally: HealthMeasureTally) async throws -> HealthMeasureTally {
        guard let first = windows.first, let last = windows.last else { return tally }
        let weighIns = try await health.weighIns(from: start(of: first), to: end(of: last)).map { WeighIn($0, in: calendar) }
        guard !weighIns.isEmpty else { return tally }
        let report = try await server.offer(weighIns)
        try Task.checkCancellation()
        return tally.adding(report)
    }

    /// Keeps the profile's age from Health's date of birth. A date the server refuses as no one's
    /// birth leaves the age as it is rather than failing the sync it rides on, every time.
    private func applyDateOfBirth() async throws {
        guard let born = await health.dateOfBirth() else { return }
        do {
            _ = try await server.apply(dateOfBirth: born)
        } catch APIError.invalid, APIError.refused {
            return
        }
    }

    private func start(of window: DayRange) -> Date { window.from.startDate(in: calendar) }
    private func end(of window: DayRange) -> Date { window.to.adding(days: 1, in: calendar).startDate(in: calendar) }

    /// One window read from Health and its days offered, in requests of `daysPerOffer`.
    private func synced(_ window: DayRange, after tally: HealthMeasureTally) async throws -> HealthMeasureTally {
        let reduced = DailyMeasures.reduced(
            try await health.readings(from: start(of: window), to: end(of: window)), in: calendar)
        var tally = tally.leavingOut(readingsFromOtherApps: reduced.fromOtherApps)
        for start in stride(from: 0, to: reduced.days.count, by: Self.daysPerOffer) {
            let batch = Array(reduced.days[start..<min(start + Self.daysPerOffer, reduced.days.count)])
            tally = tally.adding(try await server.offer(batch))
            try Task.checkCancellation()
        }
        return tally
    }
}
