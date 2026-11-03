import Foundation
import Observation

/// Whether Lionheart's server is answering, learned from the requests the app actually makes.
///
/// Not the phone's network state. The server is one Mac, reached over Tailscale or the home
/// network, and it can be asleep or restarting while the phone has full signal — so the only
/// honest test is whether a request got an answer. `APIClient` reports every exchange here: any
/// HTTP status at all means reachable, a request that never completed means not.
///
/// While the server is unreachable and the app is in the foreground, it asks again on a widening
/// interval with one cheap read, so the app notices the server's return without the athlete
/// pulling to refresh. The moment it does, `onReturn` runs: the workout's sync, the outbox and
/// every screen showing what it last knew all go again.
@Observable
@MainActor
final class ServerReachability {
    /// What the last exchange said. Reachable until something says otherwise, so a launch with no
    /// history does not start out warning about a server it has not tried yet.
    private(set) var isReachable = true

    /// How long to wait before each probe while the server stays away: soon at first, since a
    /// dropped signal usually comes back within seconds, then no more often than once a minute.
    static let probeIntervals: [Duration] = [.seconds(5), .seconds(10), .seconds(20), .seconds(40), .seconds(60)]

    private var probe: (@Sendable () async -> Void)?
    private var returnHandlers: [@MainActor () -> Void] = []
    private var isInForeground = true
    private var probing: Task<Void, Never>?

    /// The one cheap read made to ask whether the server is back. It goes through `APIClient`
    /// like any other, which is what reports its outcome here.
    func probe(with read: @escaping @Sendable () async -> Void) {
        probe = read
    }

    /// Hands `handler` the job of picking up whatever waited on the server, when it returns.
    func onReturn(_ handler: @escaping @MainActor () -> Void) {
        returnHandlers.append(handler)
    }

    /// Records what one exchange with the server came to.
    func record(answered: Bool) {
        guard answered != isReachable else { return }
        isReachable = answered
        if answered {
            probing?.cancel()
            probing = nil
            for handler in returnHandlers { handler() }
        } else {
            startProbing()
        }
    }

    /// The app came to the foreground: an unreachable server is asked again now rather than on
    /// the interval, since coming back to the app is the moment the athlete expects it to be
    /// current.
    func enteredForeground() {
        isInForeground = true
        guard !isReachable else { return }
        probing?.cancel()
        probing = nil
        startProbing(immediately: true)
    }

    /// The app left the foreground. Probing stops: the system would suspend it anyway, and
    /// coming back starts it again.
    func leftForeground() {
        isInForeground = false
        probing?.cancel()
        probing = nil
    }

    private func startProbing(immediately: Bool = false) {
        guard probing == nil, isInForeground, let probe else { return }
        let intervals = immediately ? [.zero] + Self.probeIntervals : Self.probeIntervals

        probing = Task { [weak self] in
            for attempt in 0... {
                let wait = intervals[min(attempt, intervals.count - 1)]
                do {
                    try await Task.sleep(for: wait)
                } catch {
                    return
                }
                guard let self, !self.isReachable else { return }
                await probe()
            }
        }
    }
}
