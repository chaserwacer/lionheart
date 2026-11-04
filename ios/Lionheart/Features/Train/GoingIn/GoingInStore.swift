import Foundation
import Observation

/// The going-in state and what is next, for the logging surface's header.
///
/// Its own store rather than a part of the active workout's, because both are server reads and the
/// workout is the one thing the device owns: a read failing must never touch the document a set is
/// written to. Each read keeps its own load state, so a failed "next" never blanks the rating.
@Observable
@MainActor
final class GoingInStore {
    /// How long a read stands before opening the surface reads again. The state moves when the ring
    /// syncs or a session lands, not by the minute, and the surface is opened many times a session.
    private static let freshFor: TimeInterval = 10 * 60

    private(set) var state: LoadState<GoingInState> = .loading
    private(set) var next: LoadState<NextSessions> = .loading

    private let api: GoingInAPI
    private let now: @Sendable () -> Date
    private var readAt: Date?
    private var load: Task<Void, Never>?

    init(api: GoingInAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.api = api
        self.now = now
    }

    /// Reads both again unless the last read is recent and of today.
    func refreshIfStale() async {
        if let readAt, now().timeIntervalSince(readAt) < Self.freshFor,
           CalendarDay(readAt) == CalendarDay(now()), state.value != nil {
            return
        }
        await refresh()
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
        readAt = nil
        state = .loading
        next = .loading
    }

    private func reload() async {
        let api = api
        let today = CalendarDay(now())
        async let going = Self.fetched { try await api.goingIn(remembering: .goingIn) }
        async let coming = Self.fetched { try await api.next(on: today, remembering: .nextSessions) }
        let (goingIn, nextUp) = await (going, coming)
        state = Self.applied(goingIn, to: state)
        next = Self.applied(nextUp, to: next)
        // A recalled answer is the last one the server gave, so it does not count as a fresh read.
        if case .success(.current) = goingIn { readAt = now() }
    }

    /// One read's outcome. `nil` is a cancellation, which changes nothing.
    private nonisolated static func fetched<Value: Sendable>(
        _ fetch: @Sendable () async throws -> Remembered<Value>
    ) async -> Result<Remembered<Value>, LoadFailure>? {
        do {
            return .success(try await fetch())
        } catch is CancellationError {
            return nil
        } catch {
            return .failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    private static func applied<Value>(
        _ outcome: Result<Remembered<Value>, LoadFailure>?, to state: LoadState<Value>
    ) -> LoadState<Value> {
        switch outcome {
        case .success(let read): state.applying(read)
        case .failure(let failure): state.applying(.failure(failure))
        case nil: state
        }
    }
}
