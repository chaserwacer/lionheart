import Foundation
import Observation

/// Everything the History tab has: the session log, the records, and the trends drawn from the
/// same window.
///
/// Three requests in parallel (`async let`) because the screen needs all three and their count is
/// known. The movement catalogue is one of them and is not optional dressing: a set carries only a
/// `movementDataId`, so without the catalogue every row in the log reads "Unknown movement".
///
/// The window is one read that the log *and* the trends are derived from, so a session cannot
/// appear in the log and be missing from the chart.
@Observable
@MainActor
final class HistoryStore {
    /// How far back the log reaches. A year is what a lifter scrolls and what makes a trend a
    /// trend; it is also small enough to be one request rather than a paged one.
    private static let lookBackDays = 365
    /// Far enough forward to show what is planned, which belongs below the past in the same log.
    private static let lookAheadDays = 45

    private(set) var state: LoadState<HistorySnapshot> = .loading

    private let sessions: TrainingSessionsAPI
    private let movements: MovementsAPI
    private let records: PersonalRecordsAPI
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?

    init(
        sessions: TrainingSessionsAPI,
        movements: MovementsAPI,
        records: PersonalRecordsAPI,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.sessions = sessions
        self.movements = movements
        self.records = records
        self.now = now
    }

    /// Loads the tab, replacing any load already running so a pull to refresh during a slow first
    /// load cannot leave two responses racing to set the state.
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
    }

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)

        do {
            async let window = sessions.sessions(
                from: moment.addingTimeInterval(-Double(Self.lookBackDays) * 86_400),
                to: moment.addingTimeInterval(Double(Self.lookAheadDays) * 86_400))
            async let catalogue = movements.movements()
            async let byMovement = records.recordsByMovement()

            let snapshot = HistorySnapshot.reading(
                sessions: try await window,
                movements: try await catalogue,
                records: try await byMovement,
                today: today)
            state = state.applying(.success(snapshot))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
