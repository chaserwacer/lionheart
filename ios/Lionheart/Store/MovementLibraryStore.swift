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

    /// Ids a current catalogue was read for and still did not hold: the server does not list them,
    /// so asking again would answer the same. Not observed, because nothing renders from it.
    @ObservationIgnored private var unlisted: Set<UUID> = []
    /// Ids a name was asked for and not found, already handed to `cover`.
    @ObservationIgnored private var noticed: Set<UUID> = []
    /// The catalogue read `cover` has out, which a second caller waits on rather than repeating.
    @ObservationIgnored private var covering: Task<Void, Never>?

    init(movements: MovementsAPI, records: PersonalRecordsAPI, defaults: UserDefaults = .standard) {
        self.movements = movements
        self.personalRecords = records
        self.defaults = defaults
    }

    /// The whole catalogue, or nothing while it is still loading.
    var all: [MovementData] { state.value ?? [] }

    /// The catalogue as names by id, for a screen naming the sets it holds.
    var catalogue: MovementCatalogue { MovementCatalogue(all) }

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
    ///
    /// A miss is also the signal that the catalogue is behind — a movement the Strong import or the
    /// web app created after it was read — so it hands the id to `cover`, which reads the
    /// catalogue again once. The name then arrives on the next render, and the placeholder stays
    /// only for a movement the server genuinely does not list.
    func title(of id: UUID?) -> String {
        guard let id else { return "" }
        if let movement = movement(id) { return movement.title }
        notice(id)
        return "Unknown movement"
    }

    /// Makes sure the catalogue names every one of `ids`, reading it again — once — when it does
    /// not.
    ///
    /// Screens that hold sets call this with the movements they reference, because a catalogue
    /// loaded at sign-in knows nothing of a movement made since. An id the server still does not
    /// list after a current read is remembered as unlisted, so a deleted movement costs one read and
    /// not one per render. A read that could not reach the server proves nothing about an id, so
    /// it leaves it to be asked about again.
    func cover(_ ids: Set<UUID>) async {
        if let running = covering { await running.value }
        if state.value == nil { await refreshIfNeeded() }

        let missing = catalogue.missing(ids).subtracting(unlisted)
        guard !missing.isEmpty else { return }

        let task = Task {
            await self.refresh()
            if case .loaded = self.state {
                self.unlisted.formUnion(self.catalogue.missing(missing))
            }
        }
        covering = task
        await task.value
        // Only its own handle: a second caller may have started the next read meanwhile.
        if covering == task { covering = nil }
    }

    /// Hands an id a name was not found for to `cover`, once. Deferred to a task of its own because
    /// it is reached from a view's body, where reading the catalogue again must not happen inline.
    private func notice(_ id: UUID) {
        guard state.value != nil, !unlisted.contains(id), noticed.insert(id).inserted else { return }
        Task { await self.cover([id]) }
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

    /// Loads only if nothing current is loaded yet, which is what a picker being opened wants: the
    /// catalogue is not worth a round trip every time a sheet appears.
    func refreshIfNeeded() async {
        guard state.value == nil || state.isStale else { return }
        await refresh()
    }

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        records = [:]
        unlisted = []
        noticed = []
        covering?.cancel()
        covering = nil
        defaults.removeObject(forKey: Self.recentsKey)
    }

    private func reload() async {
        do {
            // Independent, both wanted, a known number of them.
            // Remembered, because the picker and the record badges are needed most where there is
            // no signal: a workout started in a basement has to have movements to log against.
            async let catalogue = movements.movements(remembering: .movements)
            async let summaries = personalRecords.summaries(remembering: .recordSummaries)

            let loaded = try await catalogue
            let found = try await summaries

            state = state.applying(loaded)
            records = Dictionary(
                found.value.map { ($0.subject, $0) }, uniquingKeysWith: { first, _ in first })
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
