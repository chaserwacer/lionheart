import Foundation
import Observation

/// Where training stands this week: the volume and what is up next, the priorities, and the
/// training score with its weeks before.
///
/// One store for the four reads because Train, Today and the priorities screen show them side by
/// side and a session finished on one tab must move all of them. Each keeps its own load state, so
/// a failed score read never blanks the volume already on screen. Supplied to its screens through
/// the environment, so the one-line Today section and the History link reach it without either
/// screen's initialiser changing.
@Observable
@MainActor
final class TrainingBalanceStore {
    private(set) var volume: LoadState<TrainingVolume> = .loading
    private(set) var priorities: LoadState<PriorityReport> = .loading
    private(set) var score: LoadState<TrainingScore> = .loading
    private(set) var weeks: LoadState<[WeeklyScore]> = .loading

    /// How many weeks of scores the trend reads: half a year, enough to see a block and the gap
    /// after one.
    static let trendWeeks = 26

    private let balance: TrainingBalanceAPI
    private let focus: FocusAPI
    private let now: @Sendable () -> Date
    private var load: Task<Void, Never>?

    init(balance: TrainingBalanceAPI, focus: FocusAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.balance = balance
        self.focus = focus
        self.now = now
    }

    var today: CalendarDay { CalendarDay(now()) }

    var isWaitingOnServer: Bool {
        volume.isWaitingOnServer || priorities.isWaitingOnServer || score.isWaitingOnServer
            || weeks.isWaitingOnServer
    }

    /// Reads all four again, replacing any read already running.
    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func refreshIfNeeded() async {
        guard volume.value == nil || score.value == nil else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        volume = .loading
        priorities = .loading
        score = .loading
        weeks = .loading
    }

    private func reload() async {
        let today = self.today
        await withDiscardingTaskGroup { group in
            group.addTask { await self.loadVolume(endingOn: today) }
            group.addTask { await self.loadPriorities(endingOn: today) }
            group.addTask { await self.loadScore(endingOn: today) }
            group.addTask { await self.loadWeeks(endingOn: today) }
        }
    }

    private func loadVolume(endingOn today: CalendarDay) async {
        let balance = balance
        let read = await Self.fetched { try await balance.volume(endingOn: today, remembering: .trainingVolume) }
        volume = Self.next(volume, read)
    }

    private func loadPriorities(endingOn today: CalendarDay) async {
        let focus = focus
        let read = await Self.fetched { try await focus.priorities(endingOn: today, remembering: .priorities) }
        priorities = Self.next(priorities, read)
    }

    private func loadScore(endingOn today: CalendarDay) async {
        let balance = balance
        let read = await Self.fetched { try await balance.score(endingOn: today, remembering: .trainingScore) }
        score = Self.next(score, read)
    }

    private func loadWeeks(endingOn today: CalendarDay) async {
        let balance = balance
        let range = DayRange(from: today.adding(days: -7 * Self.trendWeeks), to: today)
        let read = await Self.fetched { try await balance.weeks(range, remembering: .trainingScoreWeeks) }
        weeks = Self.next(weeks, read)
    }

    /// What one read came to: an answer, a failure to state, or a cancellation that changes nothing.
    private enum Fetched<Value: Sendable>: Sendable {
        case read(Remembered<Value>)
        case failed(LoadFailure)
        case cancelled
    }

    private nonisolated static func fetched<Value: Sendable>(
        _ fetch: @Sendable () async throws -> Remembered<Value>
    ) async -> Fetched<Value> {
        do {
            return .read(try await fetch())
        } catch is CancellationError {
            return .cancelled
        } catch {
            return .failed((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    /// A read applied to its state; a failure keeps the value already there.
    private static func next<Value>(_ state: LoadState<Value>, _ fetched: Fetched<Value>) -> LoadState<Value> {
        switch fetched {
        case .read(let read): state.applying(read)
        case .failed(let failure): state.applying(.failure(failure))
        case .cancelled: state
        }
    }
}
