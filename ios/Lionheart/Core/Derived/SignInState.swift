import Foundation

/// Why a sign-in attempt did not produce a session.
///
/// Identity's vocabulary, not HTTP's: the view renders these, and that is the whole reason
/// `APIError` is restated here rather than being matched on further up.
enum SignInFailure: Equatable, Sendable {
    /// Identity answers 401 for a wrong password and for an email it has never seen alike, so
    /// one case covers both — and saying which would tell a stranger which addresses have
    /// accounts.
    case credentialsRejected
    /// The account exists but Identity will not issue a token for it yet — an unconfirmed email,
    /// a lockout.
    case accountNotReady(String)
    case offline(String)
    case unavailable(String)

    /// Restates a network failure as a sign-in failure, once, at this boundary.
    ///
    /// Cancellation is rethrown rather than classified: a view that went away while the request
    /// was in flight has nothing to report.
    static func from(_ error: any Error) throws -> SignInFailure {
        if error is CancellationError { throw CancellationError() }
        guard let api = error as? APIError else { return .unavailable(String(describing: error)) }

        switch api {
        case .unauthenticated, .forbidden:
            return .credentialsRejected
        case .invalid(let fields, let detail):
            return .accountNotReady(Self.message(fields: fields, detail: detail))
        case .transport(let description):
            return .offline(description)
        case .notFound(let detail), .refused(let detail), .upstream(let detail), .server(let detail):
            return .unavailable(detail ?? "The server could not sign you in.")
        case .http(let status, let detail):
            return .unavailable(detail ?? "The server answered \(status).")
        case .decoding(let description):
            return .unavailable(description)
        }
    }

    /// What the athlete reads.
    var message: String {
        switch self {
        case .credentialsRejected: "That email and password did not match an account."
        case .accountNotReady(let reason): reason
        case .offline: "Tempo is not reachable. Check that the backend is running."
        case .unavailable(let reason): reason
        }
    }

    private static func message(fields: [String: [String]], detail: String?) -> String {
        let fromFields = fields.values.flatMap(\.self).first
        return fromFields ?? detail ?? "Those details were not accepted."
    }
}

/// Where a sign-in attempt stands.
///
/// `.submitting` is a case rather than a Boolean beside the failure because the three states are
/// mutually exclusive and the form disables itself in exactly one of them.
enum SignInState: Equatable, Sendable {
    case editing
    case submitting
    case refused(SignInFailure)

    var isSubmitting: Bool {
        switch self {
        case .submitting: true
        case .editing, .refused: false
        }
    }

    var failure: SignInFailure? {
        switch self {
        case .editing, .submitting: nil
        case .refused(let failure): failure
        }
    }
}

/// Whether a set of typed credentials is worth sending.
///
/// Deliberately thin: the server owns what a valid password is, and a client that second-guesses it
/// ends up refusing credentials the server would have accepted. This only catches the empty form.
enum Credentials {
    static func areSubmittable(email: String, password: String) -> Bool {
        !email.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty && !password.isEmpty
    }
}
