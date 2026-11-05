import Foundation

/// Why an Apple Health sync stopped, in the sync's own words.
///
/// A workout the server refuses is not here: that is one workout's answer, counted in the tally,
/// and the sync goes on to the next. These are the failures that stop the whole sync — the server
/// out of reach, or Health itself refusing a read — where carrying on would only fail the same way.
enum HealthSyncFailure: Error, Equatable, Sendable {
    case offline
    /// Health could not be read; its own description.
    case healthUnreadable(String)
    /// The server answered with a fault; its own sentence.
    case unavailable(String)

    /// Restates a failure once, at the boundary. Cancellation is rethrown: a sync abandoned because
    /// the session ended is not something to report.
    static func from(_ error: any Error) throws -> HealthSyncFailure {
        if error is CancellationError { throw CancellationError() }
        guard let api = error as? APIError else { return .healthUnreadable(String(describing: error)) }

        switch api {
        case .transport:
            return .offline
        case .unauthenticated, .forbidden:
            return .unavailable("You are not signed in.")
        case .refused(let detail), .notFound(let detail), .server(let detail), .http(_, let detail),
             .upstream(let detail), .invalid(_, let detail):
            return .unavailable(detail ?? "The server did not take the workout.")
        case .decoding(let description):
            return .unavailable(description)
        }
    }

    /// The server's reason, when it refused this one workout rather than the sync: a workout it
    /// could not read (400), or one it would not write (409). Anything else stops the sync.
    static func refusalOfOneWorkout(_ error: any Error) -> String? {
        switch error as? APIError {
        case .invalid(_, let detail): detail ?? "The server could not read the workout."
        case .refused(let detail): detail ?? "The server did not take the workout."
        case .unauthenticated, .forbidden, .notFound, .upstream, .server, .http, .transport, .decoding, nil: nil
        }
    }

    /// The short line the row leads with.
    var headline: String {
        switch self {
        case .offline: "Tempo is not reachable"
        case .healthUnreadable: "Health could not be read"
        case .unavailable: "Sync failed"
        }
    }

    var detail: String? {
        switch self {
        case .offline: nil
        case .healthUnreadable(let detail), .unavailable(let detail): detail
        }
    }
}
