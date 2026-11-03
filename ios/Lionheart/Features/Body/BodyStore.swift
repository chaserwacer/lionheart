import Foundation
import Observation

/// The Body tab's data: Oura days, wellness entries, and injuries.
///
/// Three requests in parallel (`async let`) — the count is known, the screen needs all three, and
/// none of them is a model call, so there is nothing to serialise here.
///
/// The Oura read is the **range** route and never the single-day one. That route answers 200 with a
/// synthesized placeholder for a day with no data — a zero readiness and an empty object id — so a
/// client asking for today cannot tell "not recovered" from "the ring has not synced". The range
/// route returns only rows that exist, which is the only read that can say "no data" honestly.
@Observable
@MainActor
final class BodyStore {
    /// How far back the screen reaches. Eight weeks is enough to see a trend in sleep and
    /// readiness and short enough to be one request.
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
        let earliest = CalendarDay(moment.addingTimeInterval(-Double(Self.lookBackDays) * 86_400))

        do {
            async let days = oura.days(from: earliest, to: today)
            async let readings = wellness.states(from: earliest, to: today)
            // Every injury on record, active or not: there is no server-side filter, and a
            // resolved injury is still history worth reading. The snapshot narrows.
            async let tracked = injuries.injuries()

            let snapshot = BodySnapshot.reading(
                ouraDays: try await days,
                wellness: try await readings,
                injuries: try await tracked)
            state = state.applying(.success(snapshot))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
