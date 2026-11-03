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

    /// Where a transition asked for from a row stands. Separate from `state`, which is about the
    /// screen's data: a skip that the server refuses must not make the log unavailable.
    private(set) var action: ActionState = .idle

    /// Each record chain a pushed history screen has asked for, by key. Held here rather than in
    /// the screen so a retraction made on it re-reads the chain it was made from.
    private(set) var histories: [RecordKey: LoadState<RecordHistory>] = [:]

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
        action = .idle
        histories = [:]
    }

    // MARK: - Acting on a row

    /// Planned → Skipped, from a row.
    func skip(_ sessionId: UUID) async {
        await write { _ = try await self.sessions.skip(id: sessionId) }
    }

    /// Removes the session entirely — the remedy for one logged by mistake, and the only remedy for
    /// one skipped by mistake, since there is no route back from skipped.
    func delete(_ sessionId: UUID) async {
        await write { try await self.sessions.delete(id: sessionId) }
    }

    /// Withdraws a standing record, then re-reads both the records and the chain it belonged to.
    ///
    /// The outcome is not applied by hand: whether the record it beat stands again or nothing
    /// does, the summaries say so, and splicing the restored record in would be a second account
    /// of the chain. A 409 — the record was already beaten or withdrawn elsewhere — is kept and
    /// stated with the server's own sentence, and the chain is re-read either way so the screen
    /// shows what does stand.
    func retract(_ record: PersonalRecord) async {
        await write { _ = try await self.records.retract(record.personalRecordId) }
        if histories[record.key] != nil {
            await loadHistory(of: record.key)
        }
    }

    /// Reads one record chain. A failed re-read keeps the chain already shown.
    ///
    /// The state is read again after the await rather than before it: a retraction's re-read and
    /// the screen's own can overlap, and applying to a stale copy would let the slower failure
    /// blank a chain the faster one had just loaded.
    func loadHistory(of key: RecordKey) async {
        let outcome: Result<RecordHistory, LoadFailure>
        do {
            outcome = .success(RecordHistory.reading(try await records.history(of: key), of: key))
        } catch is CancellationError {
            return
        } catch {
            outcome = .failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
        histories[key] = (histories[key] ?? .loading).applying(outcome)
    }

    /// One transition, then a re-read of the window.
    ///
    /// Re-reading rather than editing the snapshot in place: a transition answers with the session
    /// in its new state, but the log, the trends and the records are one reading of one window, and
    /// splicing a session into the first would leave the other two disagreeing with it.
    ///
    /// A refusal is kept and stated. Either of these can be refused for a reason the athlete needs
    /// to hear — most often that the session was already started or skipped somewhere else, which
    /// is a 409 — and a context-menu action that swallowed it would look exactly like one that
    /// worked, since the row it acted on is still there either way.
    private func write(_ transition: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running

        do {
            try await transition()
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
        }
    }

    func dismissRefusal() {
        action = .idle
    }

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)

        do {
            async let window = sessions.sessions(
                from: moment.addingTimeInterval(-Double(Self.lookBackDays) * 86_400),
                to: moment.addingTimeInterval(Double(Self.lookAheadDays) * 86_400))
            async let catalogue = movements.movements()
            async let bySubject = records.summaries()

            let snapshot = HistorySnapshot.reading(
                sessions: try await window,
                movements: try await catalogue,
                records: try await bySubject,
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
