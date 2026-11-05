import Foundation
import Observation

/// The athlete's non-training days: the calendar of the weeks around today and the statements that
/// mark it, and marking or removing one.
///
/// Read when its sheet opens rather than kept warm: it is consulted, not watched. A write re-reads
/// both, so the calendar shows the server's precedence between a date, a planned session and a
/// weekday, then tells its listeners, since every expectation on Train moves with the days.
@Observable
@MainActor
final class NonTrainingDaysStore {
    private(set) var calendar: LoadState<AvailabilityCalendar> = .loading
    private(set) var marked: LoadState<[NonTrainingDay]> = .loading
    private(set) var action: ActionState = .idle

    /// The days the calendar offers: four weeks back, so a week of travel just past can still be
    /// marked, and four months ahead.
    static let daysBack = 28
    static let daysAhead = 120

    private let api: AvailabilityAPI
    private let now: @Sendable () -> Date
    private var whenChanged: [@MainActor () async -> Void] = []
    private var load: Task<Void, Never>?

    init(api: AvailabilityAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.api = api
        self.now = now
    }

    var today: CalendarDay { CalendarDay(now()) }

    var range: DayRange {
        DayRange(from: today.adding(days: -Self.daysBack), to: today.adding(days: Self.daysAhead))
    }

    /// Runs after every change the server takes, for the screens whose targets read the days.
    func onChanged(_ action: @escaping @MainActor () async -> Void) {
        whenChanged.append(action)
    }

    /// Reads the calendar and the statements again, replacing any read already running.
    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    private func reload() async {
        let api = api
        let range = range
        async let calendarRead = Self.read { try await api.availability(range) }
        async let markedRead = Self.read { try await api.nonTrainingDays() }
        let (calendarResult, markedResult) = await (calendarRead, markedRead)
        calendar = Self.applying(calendarResult, to: calendar)
        marked = Self.applying(markedResult, to: marked)
    }

    func reset() {
        load?.cancel()
        load = nil
        calendar = .loading
        marked = .loading
        action = .idle
    }

    /// Marks a day, or its weekday every week, non-training.
    func mark(_ draft: NonTrainingDayDraft) async {
        await write { _ = try await self.api.mark(draft) }
    }

    /// Removes a statement whole; a weekly one frees every day it covered.
    func remove(_ nonTrainingDayId: UUID) async {
        await write { try await self.api.remove(nonTrainingDayId) }
    }

    func dismissRefusal() {
        action = .idle
    }

    /// The write, then a re-read either way — a refusal here means the calendar was out of date —
    /// with the action running until the re-read lands, so the calendar takes no tap read against
    /// the days before it.
    private func write(_ change: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running
        let outcome: ActionState
        do {
            try await change()
            outcome = .idle
        } catch {
            outcome = ActionState.refusal(from: error)
        }
        await refresh()
        action = outcome
        for listener in whenChanged { await listener() }
    }

    private nonisolated static func read<Value: Sendable>(
        _ fetch: @Sendable () async throws -> Value
    ) async -> Result<Value, LoadFailure>? {
        do {
            return .success(try await fetch())
        } catch is CancellationError {
            return nil
        } catch {
            return .failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    /// A read applied to its state; a failure keeps the value already there, a cancellation changes nothing.
    private static func applying<Value>(_ read: Result<Value, LoadFailure>?, to state: LoadState<Value>) -> LoadState<Value> {
        read.map(state.applying) ?? state
    }
}
