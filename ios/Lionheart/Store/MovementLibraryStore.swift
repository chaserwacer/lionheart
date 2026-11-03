import Foundation
import Observation

/// The athlete's movements, and the records standing against them.
///
/// It lives in `Store/` rather than in a feature because three places read it: the picker, the set
/// rows (which turn a movement id into a name), and the workout bar. Loading it three times would
/// mean three lists that can disagree, and the catalogue changes about once a month.
///
/// The records are loaded with it and refreshed when a workout is finished, not during one:
/// `SetRecordOutcome` judges a set against the records as they stood when the workout began plus the
/// sets logged since, which is the whole of what the device knows until the server has the session.
@Observable
@MainActor
final class MovementLibraryStore {
    /// How many recently-used movements to keep. Enough to cover a session's worth of movements
    /// without the list needing to be scrolled, which is the entire point of it.
    private static let recentLimit = 8

    private static let recentsKey = "lh.movements.recent"

    private(set) var state: LoadState<[MovementData]> = .loading

    /// The standing records, by subject. A dictionary rather than a list because every read is by
    /// subject — and trail running is a subject, so a run logged mid-workout looks up the same way
    /// a lift does.
    private(set) var records: [RecordSubject: SubjectRecords] = [:]

    private let movements: MovementsAPI
    private let personalRecords: PersonalRecordsAPI
    private let defaults: UserDefaults
    private var load: Task<Void, Never>?

    init(movements: MovementsAPI, records: PersonalRecordsAPI, defaults: UserDefaults = .standard) {
        self.movements = movements
        self.personalRecords = records
        self.defaults = defaults
    }

    /// The whole catalogue, or nothing while it is still loading.
    var all: [MovementData] { state.value ?? [] }

    /// The movements used most recently, most recent first, filtered to ones that still exist.
    ///
    /// Recency is the device's own list, not a server derivation. The server could answer it from
    /// the session history, but the answer is wanted in a basement halfway through a workout and
    /// one more round trip is one more thing to be without.
    var recent: [MovementData] {
        // Uniquing rather than `uniqueKeysWithValues`, which traps on a duplicate key: the ids come
        // from the server, and a crash in a picker is not the right answer to a catalogue that
        // somehow listed one twice.
        let byId = Dictionary(all.map { ($0.movementDataId, $0) }, uniquingKeysWith: { first, _ in first })
        return recentIds.compactMap { byId[$0] }
    }

    private var recentIds: [UUID] {
        (defaults.array(forKey: Self.recentsKey) as? [String] ?? []).compactMap(UUID.init(uuidString:))
    }

    /// The movement with this id. Optional in and optional out, because most callers are holding
    /// an id that may not be there — a set with no movement, or a workout between movements.
    func movement(_ id: UUID?) -> MovementData? {
        guard let id else { return nil }
        return all.first { $0.movementDataId == id }
    }

    /// The title for a movement id, or a plain stand-in.
    ///
    /// A set logged against a movement the catalogue no longer lists still has to render — the
    /// movement could have been deleted on the web app mid-workout — so this is total rather than
    /// optional.
    func title(of id: UUID?) -> String {
        guard let id else { return "" }
        return movement(id)?.title ?? "Unknown movement"
    }

    /// The records standing on the subject a set competes on, or `nil` for a set that has none.
    func records(for command: SetCommand) -> SubjectRecords? {
        command.recordSubject.flatMap { records[$0] }
    }

    func records(on movementDataId: UUID) -> SubjectRecords? { records[.movement(movementDataId)] }

    /// Remembers that a movement was just used, moving it to the front.
    func noteUsed(_ id: UUID) {
        let next = ([id] + recentIds.filter { $0 != id }).prefix(Self.recentLimit)
        defaults.set(next.map(\.uuidString), forKey: Self.recentsKey)
    }

    /// Forgets that a movement was used, taking it out of the recent list.
    ///
    /// The recent list exists to save taps mid-workout, so one movement opened by mistake makes it
    /// worse rather than better. Removing it is the device's own business — recency is not a server
    /// derivation — and the movement itself is untouched.
    func forgetUsed(_ id: UUID) {
        defaults.set(recentIds.filter { $0 != id }.map(\.uuidString), forKey: Self.recentsKey)
    }

    /// Loads the catalogue and the records together, replacing any load already running.
    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    /// Loads only if nothing is loaded yet, which is what a picker being opened wants: the
    /// catalogue is not worth a round trip every time a sheet appears.
    func refreshIfNeeded() async {
        guard state.value == nil else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        records = [:]
        defaults.removeObject(forKey: Self.recentsKey)
    }

    private func reload() async {
        do {
            // Independent, both wanted, a known number of them.
            async let catalogue = movements.movements()
            async let summaries = personalRecords.summaries()

            let loaded = try await catalogue
            let found = try await summaries

            state = state.applying(.success(loaded))
            records = Dictionary(
                found.map { ($0.subject, $0) }, uniquingKeysWith: { first, _ in first })
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
