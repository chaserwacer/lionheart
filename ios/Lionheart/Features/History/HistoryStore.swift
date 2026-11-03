import Foundation
import Observation

/// Everything the History tab has: the session log, the records, and the readings drawn from the
/// same window — and, because the window is the whole log, every movement's history too.
///
/// It is the app's one reading of the log: Today opens a movement's page and a session's records
/// from it as well, so the two tabs cannot show a movement two ways.
///
/// The window, the records and the movement catalogue are read in parallel (`async let`). The
/// catalogue is the app-wide one in `MovementLibraryStore` rather than a copy of its own, so Today
/// and History name a movement the same way, and a set naming a movement it does not hold asks it
/// to read itself again (`cover`) rather than reading "Unknown movement".
///
/// The window is one read that the log *and* the trends are derived from, so a session cannot
/// appear in the log and be missing from the chart.
@Observable
@MainActor
final class HistoryStore {
    /// Where the log's window begins: before anything this athlete could have logged, so the
    /// whole history is one read. An imported history reaches back years, and a log or a trend
    /// that stops at a year hides the start of it. At a personal log's size the whole of it is
    /// still one modest response, and it is what the device remembers for the tab offline.
    private static let logBegins = Date(timeIntervalSince1970: 0)
    /// Far enough forward to show what is planned, which belongs below the past in the same log.
    private static let lookAheadDays = 45

    private(set) var state: LoadState<HistorySnapshot> = .loading

    /// Where a transition asked for from a row stands. Separate from `state`, which is about the
    /// screen's data: a skip that the server refuses must not make the log unavailable.
    private(set) var action: ActionState = .idle

    /// Each record chain a pushed history screen has asked for, by key. Held here rather than in
    /// the screen so a retraction made on it re-reads the chain it was made from.
    private(set) var histories: [RecordKey: LoadState<RecordHistory>] = [:]

    /// The records a movement's history or a session established, every standing, by what was
    /// asked — what marks the sets that set one.
    private(set) var established: [EstablishedRecords: LoadState<[PersonalRecord]>] = [:]

    /// Which records were asked for: one movement's, or one session's.
    enum EstablishedRecords: Hashable, Sendable {
        case movement(UUID)
        case session(UUID)
    }

    private let sessions: TrainingSessionsAPI
    private let library: MovementLibraryStore
    private let records: PersonalRecordsAPI
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?

    init(
        sessions: TrainingSessionsAPI,
        library: MovementLibraryStore,
        records: PersonalRecordsAPI,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.sessions = sessions
        self.library = library
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

    /// Loads only if nothing is loaded yet — for a screen reached from another tab, such as a
    /// movement's page opened from Today before History has been visited.
    func refreshIfNeeded() async {
        guard state.value == nil else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        action = .idle
        histories = [:]
        established = [:]
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
        do {
            let chain = try await records.history(of: key).map { RecordHistory.reading($0, of: key) }
            histories[key] = (histories[key] ?? .loading).applying(chain)
        } catch is CancellationError {
            return
        } catch {
            histories[key] = (histories[key] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// Reads the records one movement or one session established. A failed re-read keeps what was
    /// shown, and is read again after the await for the reason `loadHistory` is.
    func loadEstablished(_ asked: EstablishedRecords) async {
        do {
            let read: Remembered<[PersonalRecord]>
            switch asked {
            case .movement(let id):
                read = try await records.established(on: id)
            case .session(let id):
                guard let session = state.value?.session(id), let start = session.startedAt else {
                    // A session with no start instant set no record, since a record is dated by it.
                    established[asked] = .loaded([])
                    return
                }
                let end = session.endedAt ?? start
                read = try await records.established(
                    from: start, to: end, remembering: .establishedRecords(session: id))
            }
            established[asked] = (established[asked] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            established[asked] = (established[asked] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
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
    /// worked, since the row it acted on is still there either way. That refusal also means the
    /// window is out of date, so it is read again; the alert stays up over the corrected log.
    private func write(_ transition: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running

        do {
            try await transition()
            action = .idle
            await refresh()
        } catch {
            action = ActionState.refusal(from: error)
            if action.revealsOutdatedScreen { await refresh() }
        }
    }

    func dismissRefusal() {
        action = .idle
    }

    /// Reads the window and the records with the catalogue, derives the snapshot, and — when a set
    /// names a movement the catalogue does not hold — has the catalogue read itself again and
    /// derives once more, so the log never ships a name it could have had.
    ///
    /// The catalogue's own staleness is not the log's: a catalogue remembered from yesterday names
    /// today's sessions perfectly well, so only the window and the records date the screen.
    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)

        do {
            async let window = sessions.sessions(
                from: Self.logBegins,
                to: moment.addingTimeInterval(Double(Self.lookAheadDays) * 86_400),
                remembering: .historySessions)
            async let bySubject = records.summaries(remembering: .recordSummaries)
            async let catalogue: Void = library.refresh()

            let (sessionsRead, recordsRead, _) = try await (window, bySubject, catalogue)
            func derived() -> Remembered<HistorySnapshot> {
                .derived(
                    HistorySnapshot.reading(
                        sessions: sessionsRead.value,
                        catalogue: library.catalogue,
                        records: recordsRead.value,
                        today: today),
                    from: [sessionsRead.asOf, recordsRead.asOf])
            }

            let first = derived()
            state = state.applying(first)
            let unnamed = library.catalogue.missing(first.value.referencedMovements)
            guard !unnamed.isEmpty else { return }
            await library.cover(unnamed)
            try Task.checkCancellation()
            state = state.applying(derived())
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
