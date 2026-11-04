import Foundation
import Observation

/// The athlete's logged bodyweights.
///
/// App-wide, because two places read it: the bodyweight screen, and the logging surface, which
/// turns a weighted dip's plate into the load moved. It reads a little over a year — the longest
/// span the trend shows — and the newest reading is what the workout uses.
@Observable
@MainActor
final class BodyweightStore {
    /// How far back the series is read. A year for the trend's longest span, and a margin so a day
    /// at the start of it still has a reading in force from before.
    static let lookBack = 400

    private(set) var state: LoadState<BodyweightSeries> = .loading
    private(set) var action: ActionState = .idle

    private let bodyweight: BodyweightAPI
    private let now: @Sendable () -> Date
    private var load: Task<Void, Never>?

    init(bodyweight: BodyweightAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.bodyweight = bodyweight
        self.now = now
    }

    /// The series as loaded, or an empty one — the logging surface reads it whether or not it is
    /// current, and no reading is a legitimate answer there.
    var series: BodyweightSeries { state.value ?? BodyweightSeries([]) }

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

    /// Logs a bodyweight for a day, replacing any already logged on it. Answers whether it landed.
    @discardableResult
    func log(_ weight: Double, _ unit: WeightUnit, on day: CalendarDay) async -> Bool {
        await write { _ = try await self.bodyweight.log(weight, unit, on: day) }
    }

    func delete(on day: CalendarDay) async {
        await write { try await self.bodyweight.delete(on: day) }
    }

    private func write(_ change: () async throws -> Void) async -> Bool {
        guard !action.isRunning else { return false }
        action = .running
        do {
            try await change()
            action = .idle
            await refresh()
            return true
        } catch {
            action = ActionState.refusal(from: error)
            return false
        }
    }

    private func reload() async {
        let today = CalendarDay(now())
        do {
            let read = try await bodyweight.measurements(
                from: today.adding(days: -Self.lookBack), to: today, remembering: .bodyweight)
            state = state.applying(read.map(BodyweightSeries.init))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
