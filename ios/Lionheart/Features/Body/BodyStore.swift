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

    func reset() {
        load?.cancel()
        load = nil
        state = .loading
    }

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)
        let start = moment.addingTimeInterval(-Double(Self.lookBackDays) * 86_400)
        let earliest = CalendarDay(start)

        do {
            async let days = oura.days(from: earliest, to: today)
            // A night beginning before midnight is filed under the waking day, so the event window
            // reaches a day further back than the day range to avoid clipping its first night.
            async let nights = oura.sleepPeriods(from: start.addingTimeInterval(-86_400), to: moment)
            async let ringWorkouts = oura.workouts(from: start, to: moment)
            async let tags = oura.tags(from: start, to: moment)
            async let readings = wellness.states(from: earliest, to: today)
            // Every injury on record, active or not: there is no server-side filter, and a
            // resolved injury is still history worth reading. The snapshot narrows.
            async let tracked = injuries.injuries()

            let snapshot = BodySnapshot.reading(
                ouraDays: try await days,
                wellness: try await readings,
                injuries: try await tracked,
                nights: try await nights,
                ringWorkouts: try await ringWorkouts,
                tags: try await tags)
            state = state.applying(.success(snapshot))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
