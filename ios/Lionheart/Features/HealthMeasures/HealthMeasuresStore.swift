import Foundation
import Observation

/// Every figure kept from Apple Health, read back for the screens that show it: Body's gait section,
/// the watch's history, and an injury's gait.
///
/// App-wide, since three places on two tabs read it. Read whole — the watch's history ended years
/// ago and an injury can be any age, so no window short of everything serves them all — which is a
/// few thousand days, read once and remembered for offline reading.
@Observable
@MainActor
final class HealthMeasuresStore {
    /// HealthKit's first year: nothing in Health is older.
    static let firstDay = CalendarDay(year: 2014, month: 1, day: 1)

    private(set) var state: LoadState<HealthMeasureHistory> = .loading

    private let api: HealthMeasuresAPI
    private let now: @Sendable () -> Date
    private var load: Task<Void, Never>?

    init(api: HealthMeasuresAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.api = api
        self.now = now
    }

    var history: HealthMeasureHistory { state.value ?? HealthMeasureHistory(series: []) }

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

    /// Reads again only if something has already read it; an unread store reads for itself when opened.
    func refreshLoaded() async {
        guard state.value != nil else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
    }

    private func reload() async {
        do {
            let read = try await api.history(from: Self.firstDay, to: CalendarDay(now()), remembering: .healthMeasures)
            state = state.applying(read)
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
