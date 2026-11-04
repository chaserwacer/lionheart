import Foundation
import Observation

/// The Body tab's data: Oura days and nights, the workouts the ring saw, what the athlete tagged,
/// wellness entries, and injuries.
///
/// Six requests in parallel (`async let`) — the count is known, the screen needs all of them, and
/// none is a model call, so there is nothing to serialise here.
///
/// Days and events are separate reads because Oura's collections are two shapes: six describe a
/// calendar day, four are events occupying clock time inside one. The day read takes a day range;
/// the event reads take instants, which is why the window is computed both ways.
@Observable
@MainActor
final class BodyStore {
    /// How far back the screen reaches.
    ///
    /// Eight weeks is enough to see a trend in sleep and readiness, and it is where the cost
    /// starts to show: measured against the athlete's real archive, a refresh over this window is
    /// about 290 KB — 71 for the days, 117 for the nights' overnight curves, 100 for the workouts
    /// the ring detects. That is one read rather than a read per screen, which is what keeps the
    /// tab's panels agreeing with each other; narrowing the *event* windows alone is the first
    /// thing to try if it ever needs to be cheaper.
    private static let lookBackDays = 56

    private(set) var state: LoadState<BodySnapshot> = .loading

    /// The first day the window reaches back to.
    var firstDay: CalendarDay { CalendarDay(now()).adding(days: 1 - Self.lookBackDays) }

    /// Where an action asked for from a row stands. Separate from `state`: an injury that will not
    /// delete must not take the tab's data down with it.
    private(set) var action: ActionState = .idle

    private let oura: OuraAPI
    private let wellness: WellnessAPI
    private let injuries: InjuriesAPI
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?

    init(
        oura: OuraAPI,
        wellness: WellnessAPI,
        injuries: InjuriesAPI,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.oura = oura
        self.wellness = wellness
        self.injuries = injuries
        self.now = now
    }

    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    /// Loads only when nothing is loaded yet, which is what coming back to the tab wants.
    ///
    /// The tab's `.task` runs again on every return to it, and a refresh over this window is about
    /// 290 KB (see `lookBackDays`) — worth paying when something could have changed, and not
    /// otherwise. Nothing on the device writes an Oura day, and the writes that touch this tab —
    /// an injury, a wellness entry — reload it for themselves, so a tab switch has nothing to learn. Pull to
    /// refresh stays the athlete's own way of asking, and a new ring sync is what it is for. A
    /// screen that is not current is asked again, since it has something to learn.
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

    // MARK: - Logging an injury

    /// Starts tracking an injury, then re-reads the tab.
    ///
    /// Thrown rather than kept in `action`, because the form asking is a sheet: the tab's alert
    /// cannot present over it, and the sheet has to stay up holding what was typed while it says
    /// why the server refused.
    ///
    /// - Throws: `LoadFailure` when the server refuses or cannot be reached; `CancellationError`
    ///   when the sheet went away first.
    func createInjury(_ draft: InjuryDraft) async throws {
        try await submit { _ = try await self.injuries.create(draft) }
    }

    /// Logs a treatment or a check-in against `injury`, then re-reads the tab.
    ///
    /// - Throws: as `createInjury(_:)`.
    func addEvent(_ draft: InjuryEventDraft, to injury: Injury) async throws {
        try await submit { _ = try await self.injuries.addEvent(draft, to: injury.injuryId) }
    }

    private func submit(_ write: () async throws -> Void) async throws {
        do {
            try await write()
        } catch {
            throw try LoadFailure.from(error)
        }
        await refresh()
    }

    // MARK: - Acting on an injury

    /// Resolves an injury or takes it back up, which is one route and two directions.
    func setTracking(of injury: Injury, to isActive: Bool) async {
        await write { _ = try await self.injuries.setTracking(of: injury, to: isActive) }
    }

    func delete(_ injury: Injury) async {
        await write { try await self.injuries.delete(id: injury.injuryId) }
    }

    /// Removes one treatment or check-in. The injury keeps its other events, and its pain chart
    /// redraws from what is left.
    func delete(_ event: InjuryEvent) async {
        await write { try await self.injuries.deleteEvent(id: event.injuryEventId) }
    }

    /// One write, then a re-read.
    ///
    /// Re-reading rather than patching the snapshot: an injury that resolves moves between two
    /// sections and a deleted event changes a chart, and the snapshot is one reading of one moment
    /// — editing a corner of it is how two parts of this tab come to disagree.
    ///
    /// A refusal is kept and stated. A 404 here is the honest answer when the injury was deleted on
    /// the web app, and a row that silently did nothing would leave the athlete tapping it again.
    private func write(_ change: () async throws -> Void) async {
        guard !action.isRunning else { return }
        action = .running

        do {
            try await change()
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

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)
        let start = moment.addingTimeInterval(-Double(Self.lookBackDays) * 86_400)
        let earliest = CalendarDay(start)

        do {
            async let days = oura.days(from: earliest, to: today, remembering: .bodyOuraDays)
            // A night beginning before midnight is filed under the waking day, so the event window
            // reaches a day further back than the day range to avoid clipping its first night.
            async let nights = oura.sleepPeriods(
                from: start.addingTimeInterval(-86_400), to: moment, remembering: .bodySleepPeriods)
            async let ringWorkouts = oura.workouts(from: start, to: moment, remembering: .bodyOuraWorkouts)
            async let tags = oura.tags(from: start, to: moment, remembering: .bodyOuraTags)
            async let readings = wellness.states(from: earliest, to: today, remembering: .bodyWellness)
            // Every injury on record, active or not: there is no server-side filter, and a
            // resolved injury is still history worth reading. The snapshot narrows.
            async let tracked = injuries.injuries(remembering: .bodyInjuries)

            let (daysRead, readingsRead, trackedRead) = try await (days, readings, tracked)
            let (nightsRead, ringWorkoutsRead, tagsRead) = try await (nights, ringWorkouts, tags)
            let snapshot = BodySnapshot.reading(
                ouraDays: daysRead.value,
                wellness: readingsRead.value,
                injuries: trackedRead.value,
                nights: nightsRead.value,
                ringWorkouts: ringWorkoutsRead.value,
                tags: tagsRead.value)
            state = state.applying(.derived(snapshot, from: [
                daysRead.asOf, readingsRead.asOf, trackedRead.asOf,
                nightsRead.asOf, ringWorkoutsRead.asOf, tagsRead.asOf,
            ]))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
