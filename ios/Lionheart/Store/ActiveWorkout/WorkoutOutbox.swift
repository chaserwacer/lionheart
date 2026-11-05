import Foundation
import Observation

/// Finished workouts the server has not taken yet, and the one thing that delivers them.
///
/// A workout finished with no signal is set down here rather than refused, so the athlete can put
/// the phone away at the end of a session in a basement and start the next one tomorrow whether
/// or not the server has answered in between. Each is delivered, oldest first, the next time the
/// server is reachable.
///
/// Its rules mirror the active workout's, because it holds the same kind of thing:
///
/// 1. **On disk before it is anywhere else.** `hold` writes, then publishes, and answers whether
///    the write landed so the caller can keep the workout where it was if it did not.
/// 2. **Only the server taking it removes it.** A delivery that cannot reach the server leaves it
///    waiting; one the server refuses leaves it refused, with the reason, for the athlete.
/// 3. **Delivering twice does no harm.** `WorkoutDelivery` recognises a workout that already
///    landed, which is what makes retrying on every return of the server safe.
@Observable
@MainActor
final class WorkoutOutbox {
    /// What is waiting, oldest finish first.
    private(set) var finished: [FinishedWorkout] = []

    private let document: FinishedWorkoutsDocument
    private let delivery: WorkoutDelivery

    /// The delivery run in progress. Held so a second request to deliver joins it rather than
    /// sending the same workout twice at once.
    private var delivering: Task<Bool, Never>?

    /// Run after a delivery run that landed at least one workout. What the server derives from a
    /// session — last time on each movement, the records, a routine's last session — has moved,
    /// and the stores holding them registered here rather than this type knowing they exist.
    private var landedHandlers: [@MainActor () async -> Void] = []

    init(document: FinishedWorkoutsDocument = FinishedWorkoutsDocument(), sessions: any WorkoutSessionWriting) {
        self.document = document
        delivery = WorkoutDelivery(sessions: sessions)
    }

    /// Whether anything is waiting that another delivery could land.
    var hasDeliverableWork: Bool { finished.contains(where: \.isWorthDelivering) }

    /// Reads whatever survived the last run. Called once, from the app's start.
    func restore() async {
        finished = await Self.read(from: document)
    }

    /// Sets a finished workout down for delivery.
    ///
    /// - Returns: whether it is on disk. Nothing is published unless it is, so a caller told
    ///   `false` still holds the only copy.
    func hold(_ workout: FinishedWorkout) -> Bool {
        guard document.write(workout) else { return false }
        finished.append(workout)
        return true
    }

    /// Registers something to run after a delivery run lands a workout.
    func onLanded(_ handler: @escaping @MainActor () async -> Void) {
        landedHandlers.append(handler)
    }

    /// Delivers every waiting workout, oldest first. Joins a run already in progress.
    func deliver() async {
        if let delivering {
            _ = await delivering.value
            return
        }
        let run = Task { await self.deliverWaiting() }
        delivering = run
        let landed = await run.value
        // A sign-out during the run cleared it; what landed then is no longer this account's news.
        let isStillThisRun = delivering == run
        if isStillThisRun { delivering = nil }
        guard landed, isStillThisRun else { return }
        for handler in landedHandlers { await handler() }
    }

    /// Throws away a workout the server would not take. Only the athlete does this, and only to
    /// one that has been refused: a waiting one is still on its way.
    func discard(_ id: UUID) {
        guard finished.contains(where: { $0.id == id && !$0.isWorthDelivering }) else { return }
        document.remove(id)
        finished.removeAll { $0.id == id }
    }

    /// Empties it, documents and all. Registered with `AuthStore`: a finished workout belongs to
    /// the account that trained it, and the next sign-in must not deliver it to another.
    func reset() {
        delivering?.cancel()
        delivering = nil
        finished = []
        document.clear()
    }

    /// - Returns: whether any workout landed, which is what the landing handlers are run for.
    private func deliverWaiting() async -> Bool {
        var landed = false
        for waiting in finished where waiting.isWorthDelivering {
            guard !Task.isCancelled else { return landed }
            do {
                try await delivery.deliver(
                    waiting.workout,
                    endingAt: waiting.endedAt,
                    mayHaveLanded: true)
            } catch is CancellationError {
                return landed
            } catch {
                switch Self.verdict(on: error) {
                case .serverAway:
                    // The ones behind it would meet the same absent server. They wait for its
                    // return together.
                    return landed
                case .tryAgainLater:
                    continue
                case .refused(let reason):
                    settle(waiting.refused(reason))
                    continue
                }
            }
            document.remove(waiting.id)
            finished.removeAll { $0.id == waiting.id }
            landed = true
        }
        return landed
    }

    /// What one failed delivery means for it and for the ones behind it.
    private enum Verdict {
        /// Nothing can land now: the server is not there, or this device cannot speak for the
        /// account. Every workout waits.
        case serverAway
        /// The server answered with a fault of its own. This one waits for the next run, and the
        /// ones behind it are tried, so one workout the server chokes on holds up no other.
        case tryAgainLater
        /// The server will not take it, and sending it again cannot change that.
        case refused(String)
    }

    /// Restates a delivery's failure, once, here: the outbox needs a finer reading than a screen,
    /// because a request the server rejected for its content must not hold up the rest for ever.
    private static func verdict(on error: any Error) -> Verdict {
        guard let api = error as? APIError else { return .tryAgainLater }
        switch api {
        case .transport, .unauthenticated, .forbidden:
            return .serverAway
        case .server, .upstream, .http, .decoding:
            return .tryAgainLater
        case .refused(let detail):
            return .refused(detail ?? "The server would not take it.")
        case .invalid(_, let detail):
            return .refused(detail ?? "The server did not accept it.")
        case .notFound:
            return .refused("What it belongs to is no longer on the server.")
        }
    }

    /// Records a refusal on disk and on screen.
    private func settle(_ refused: FinishedWorkout) {
        document.write(refused)
        finished = finished.map { $0.id == refused.id ? refused : $0 }
    }

    @concurrent
    private nonisolated static func read(from document: FinishedWorkoutsDocument) async -> [FinishedWorkout] {
        document.read()
    }
}
