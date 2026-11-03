import Foundation

/// Why an Oura sync did not happen, in the sync's own words.
///
/// The server says the two failures that matter differently: an account that cannot sync as it
/// stands — no credential stored — is a 409, and Oura itself failing is a 502, which is also how a
/// **rejected Oura token** arrives, with a detail naming the token. A 401 from the sync route is
/// never Oura's: it is this app's own session, which the token provider has already dealt with by
/// the time a failure reaches here.
enum OuraSyncFailure: Error, Equatable, Sendable {
    case offline
    /// The account cannot sync as it stands; the server's sentence says why.
    case refused(String)
    /// Oura failed or refused the stored credential; the server's sentence says which.
    case ouraFailed(String)
    case unavailable(String)

    /// Restates a failure once, at the boundary. Cancellation is rethrown: a sync abandoned because
    /// the session ended is not something to report.
    static func from(_ error: any Error) throws -> OuraSyncFailure {
        if error is CancellationError { throw CancellationError() }
        guard let api = error as? APIError else { return .unavailable(String(describing: error)) }

        switch api {
        case .transport:
            return .offline
        case .refused(let detail):
            return .refused(detail ?? "This account cannot sync Oura as it stands.")
        case .upstream(let detail):
            return .ouraFailed(detail ?? "Oura did not answer.")
        case .unauthenticated, .forbidden:
            return .unavailable("You are not signed in.")
        case .notFound(let detail), .server(let detail), .http(_, let detail):
            return .unavailable(detail ?? "The sync did not run.")
        case .invalid(_, let detail):
            return .unavailable(detail ?? "The sync range was not accepted.")
        case .decoding(let description):
            return .unavailable(description)
        }
    }

    /// The short line the control leads with.
    var headline: String {
        switch self {
        case .offline: "Lionheart is not reachable"
        case .refused: "Sync refused"
        case .ouraFailed: "Oura sync failed"
        case .unavailable: "Sync failed"
        }
    }

    /// The server's own sentence, where there is one.
    var detail: String? {
        switch self {
        case .offline: nil
        case .refused(let detail), .ouraFailed(let detail), .unavailable(let detail): detail
        }
    }
}
