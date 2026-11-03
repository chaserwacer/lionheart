import Foundation

/// What a screen has, for anything it had to ask the server for.
///
/// Four cases rather than `isLoading` + `error` + `value`, which spells eight combinations of which
/// five are nonsense and makes every view branch rediscover which three are real.
///
/// `.stale` is the case that earns its keep: **a failed refresh must never blank a value already on
/// screen.** Pull to refresh in a lift with no signal is the common case, and the right answer is
/// last week's readiness marked with when it dates from, not an empty screen. The rules live in
/// `applying(_:)` so they are pure functions rather than a decision each store re-makes.
enum LoadState<Value: Equatable & Sendable>: Equatable, Sendable {
    /// Nothing asked for yet, or the first ask is still out.
    case loading
    case loaded(Value)
    /// Never had a value, and the last attempt failed.
    case unavailable(LoadFailure)
    /// Has a value that is not current, for the reason given.
    case stale(Value, Staleness)

    /// The value this state holds, if any. `.stale` answers with it, which is the point.
    var value: Value? {
        switch self {
        case .loading, .unavailable: nil
        case .loaded(let value): value
        case .stale(let value, _): value
        }
    }

    var failure: LoadFailure? {
        switch self {
        case .loading, .loaded: nil
        case .unavailable(let failure): failure
        case .stale(_, let staleness): staleness.failure
        }
    }

    /// Whether a value is on screen that is known not to be current, which is what the server's
    /// return should replace.
    var isStale: Bool {
        switch self {
        case .stale: true
        case .loading, .loaded, .unavailable: false
        }
    }

    /// Whether the screen is short of the server's current answer, with a value or without one —
    /// what a read when the server returns could put right.
    var isWaitingOnServer: Bool {
        switch self {
        case .stale, .unavailable: true
        case .loading, .loaded: false
        }
    }

    /// Where this state goes when an attempt finishes.
    ///
    /// A success replaces whatever was there. A failure keeps a value if there is one and degrades
    /// to `.unavailable` only when there is nothing to keep.
    func applying(_ result: Result<Value, LoadFailure>) -> LoadState {
        switch result {
        case .success(let value):
            return .loaded(value)
        case .failure(let failure):
            guard let held = value else { return .unavailable(failure) }
            return .stale(held, .refreshFailed(failure))
        }
    }

    /// Where this state goes when a remembered read answers: current, or recalled from the device
    /// because the server could not be reached, in which case the value is marked with its time.
    func applying(_ reading: Remembered<Value>) -> LoadState {
        switch reading {
        case .current(let value): .loaded(value)
        case .recalled(let value, let asOf): .stale(value, .unreachable(asOf: asOf))
        }
    }
}

/// Why a value on screen is not current.
///
/// Two cases because they are two different sentences. A server that answered with a fault says
/// nothing about how old the screen is, only that it could not be refreshed; a server that could
/// not be reached at all leaves the device showing what it last knew, and the one useful thing to
/// say is when that was.
enum Staleness: Equatable, Sendable {
    /// The last refresh failed, for the reason given. The value is the last one loaded.
    case refreshFailed(LoadFailure)
    /// The server could not be reached. The value is the last answer it gave, at `asOf`.
    case unreachable(asOf: Date)

    var failure: LoadFailure {
        switch self {
        case .refreshFailed(let failure): failure
        case .unreachable: .offline("The server could not be reached.")
        }
    }
}

/// Why a screen could not get what it asked for.
///
/// The feature's vocabulary rather than HTTP's, restated once from `APIError` so no view ever
/// matches on a status code. `.notFound` reads as *not found or not yours*: the server answers 404
/// for another athlete's record deliberately, so that existence is not disclosed, and the client
/// must not invent a different meaning for it.
enum LoadFailure: Error, Equatable, Sendable {
    case offline(String)
    case notFound
    case refused(String)
    case unavailable(String)

    /// Restates a network failure, once, at the boundary.
    ///
    /// Cancellation is rethrown before anything is classified — a dismissed screen is not a
    /// failure, and letting it become one is how a navigation pop turns into a red banner.
    /// `.unauthenticated` does not appear: by the time it reaches here the token provider has
    /// already moved the app to its signed-out root, so there is no screen left to tell.
    static func from(_ error: any Error) throws -> LoadFailure {
        if error is CancellationError { throw CancellationError() }
        guard let api = error as? APIError else { return .unavailable(String(describing: error)) }

        switch api {
        case .transport(let description):
            return .offline(description)
        case .notFound:
            return .notFound
        case .refused(let detail):
            return .refused(detail ?? "That is not possible from here.")
        case .unauthenticated, .forbidden:
            return .unavailable("You are not signed in.")
        case .invalid(_, let detail):
            return .unavailable(detail ?? "The request was not accepted.")
        case .upstream(let detail):
            return .unavailable(detail ?? "A service Lionheart depends on is not responding.")
        case .server(let detail):
            return .unavailable(detail ?? "The server had a problem.")
        case .http(let status, let detail):
            return .unavailable(detail ?? "The server answered \(status).")
        case .decoding(let description):
            return .unavailable(description)
        }
    }

    /// What the athlete reads.
    var message: String {
        switch self {
        case .offline: "Lionheart is not reachable."
        case .notFound: "Not found."
        case .refused(let reason): reason
        case .unavailable(let reason): reason
        }
    }

    /// Whether offering a retry makes sense.
    var isWorthRetrying: Bool {
        switch self {
        case .offline, .unavailable: true
        case .notFound, .refused: false
        }
    }
}
