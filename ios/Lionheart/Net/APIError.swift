import Foundation

/// Everything the network layer can answer with other than a value.
///
/// The cases are the server's own `Problem` vocabulary as it reaches HTTP (`ResultToHttp` maps them
/// to 400/401/403/404/409/502/500), plus the two failures that are the client's own: a request that
/// never completed and a body that did not decode. A feature restates this into its own failure
/// type once, at the endpoint that owns the route — a view never matches on a status code.
///
/// Cancellation is deliberately **not** a case. A dismissed screen is not a failure, so
/// `APIClient` throws `CancellationError` for it and this type never has to carry it.
enum APIError: Error, Equatable, Sendable {
    /// 401. The access token was rejected and renewing it did not help.
    case unauthenticated
    /// 403. Authenticated, but not allowed.
    case forbidden
    /// 404. Reads as *not found or not yours*: ownership is checked on every route and another
    /// athlete's record is indistinguishable from one that does not exist, deliberately.
    case notFound(detail: String?)
    /// 400, with `ValidationProblemDetails.errors` keyed by field.
    case invalid(fields: [String: [String]], detail: String?)
    /// 409. A domain refusal — a transition asked for from the wrong state. The UI states it; it is
    /// never swallowed.
    case refused(detail: String?)
    /// 502. A dependency the server leans on (the model, embeddings, Oura) failed, so a retry may
    /// well work.
    case upstream(detail: String?)
    /// 500.
    case server(detail: String?)
    /// A status with no `Problem` behind it.
    case http(status: Int, detail: String?)
    /// The request did not complete: no network, a refused connection, a timeout.
    case transport(String)
    /// A 2xx whose body was not what this client expects.
    case decoding(String)

    /// Whether trying again could plausibly succeed without anything else changing. Total over the
    /// cases, so a new one cannot default into the wrong answer.
    var isTransient: Bool {
        switch self {
        case .transport, .upstream, .server: true
        case .unauthenticated, .forbidden, .notFound, .invalid, .refused, .http, .decoding: false
        }
    }

    /// Whether this answer means the credential that was sent is no longer good.
    ///
    /// Only the three that are about the credential itself. A timeout, a 502 or a body this client
    /// could not read say nothing about whether the token is still valid, and discarding it for one
    /// of those would sign the athlete out over a blip. Total, so a case added later has to decide.
    var rejectsCredential: Bool {
        switch self {
        case .unauthenticated, .forbidden, .invalid: true
        case .notFound, .refused, .upstream, .server, .http, .transport, .decoding: false
        }
    }

    /// The server's own words about what went wrong, where it sent any.
    var detail: String? {
        switch self {
        case .unauthenticated, .forbidden: nil
        case .notFound(let detail), .refused(let detail), .upstream(let detail), .server(let detail):
            detail
        case .invalid(_, let detail): detail
        case .http(_, let detail): detail
        case .transport(let description), .decoding(let description): description
        }
    }
}

/// RFC 7807 as this server writes it.
///
/// `ProblemDetails` carries the message in `detail`; `ValidationProblemDetails` adds `errors`,
/// an object of field name to messages. One decoder reads both, because a 400 is just a problem
/// document with one more member.
struct ProblemDocument: Equatable, Sendable, Decodable {
    let title: String?
    let detail: String?
    let status: Int?
    let errors: [String: [String]]

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        title = container.lenient(String.self, "title")
        detail = container.lenient(String.self, "detail")
        status = container.lenient(Int.self, "status")
        errors = container.lenient([String: [String]].self, "errors") ?? [:]
    }

    /// Something to show a person: the detail if there is one, else the title.
    var message: String? {
        if let detail, !detail.isEmpty { return detail }
        if let title, !title.isEmpty { return title }
        return nil
    }
}
