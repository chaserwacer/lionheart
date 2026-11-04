import Foundation

/// The pure parts of an HTTP exchange, shared by `APIClient` and the token refresh.
///
/// It exists so renewing a token does not have to reach back through `APIClient` — which would make
/// the client depend on the token store and the token store depend on the client. The rules about
/// what a status means and how a body decodes live here once, and both callers use them.
enum HTTP {
    enum Method: String, Sendable {
        case get = "GET"
        case post = "POST"
        case put = "PUT"
        case patch = "PATCH"
        case delete = "DELETE"
    }

    /// How long a read the app cannot do without waits for the server before giving up: a
    /// remembered screen read, and the token renewal every read waits on. A server that is there
    /// answers these in well under a second; ten is generous for a weak signal, and far short of
    /// the system's minute, which is how long a Mac asleep behind Tailscale leaves a connection
    /// hanging rather than refusing it.
    static let readPatience: TimeInterval = 10

    /// How long an answer from the model can take. A standalone conversation's message waits this
    /// long, since the server sends nothing until every round of the tool loop is done, which on a
    /// hard question passes the system's minute; timing out sooner would show the question as
    /// unsent while the server went on to answer and store it. The thread, whose answer streams,
    /// looks for an answer whose connection dropped for this long.
    static let modelPatience: TimeInterval = 300

    static func request(
        _ method: Method,
        path: String,
        query: [URLQueryItem] = [],
        baseURL: URL,
        body: Data? = nil
    ) -> URLRequest {
        var components = URLComponents(
            url: baseURL.appending(path: path),
            resolvingAgainstBaseURL: false) ?? URLComponents()
        if !query.isEmpty { components.queryItems = query }

        var request = URLRequest(url: components.url ?? baseURL.appending(path: path))
        request.httpMethod = method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        if let body {
            request.httpBody = body
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }
        return request
    }

    /// The error a status carries, or `nil` when the response succeeded.
    ///
    /// Total over the statuses the server can answer with, so a code nobody planned for still
    /// becomes a failure a caller can see rather than a success with an unreadable body.
    static func failure(status: Int, body: Data) -> APIError? {
        guard !(200..<300).contains(status) else { return nil }

        let problem = try? Wire.decoder().decode(ProblemDocument.self, from: body)
        let detail = problem?.message

        if isGatewayStandIn(status: status, problem: problem) {
            return .transport("Tempo's server is not answering behind its gateway.")
        }

        switch status {
        case 400: return .invalid(fields: problem?.errors ?? [:], detail: detail)
        case 401: return .unauthenticated
        case 403: return .forbidden
        case 404: return .notFound(detail: detail)
        case 409: return .refused(detail: detail)
        case 502: return .upstream(detail: detail)
        case 500, 503, 504: return .server(detail: detail)
        default: return .http(status: status, detail: detail)
        }
    }

    /// Whether a response came from the gateway in front of the server rather than the server.
    ///
    /// The server is reached through `tailscale serve`, which answers a 502 of its own when the
    /// backend behind it is not listening — during a deploy, a crash loop, or a startup that threw.
    /// The server's own 502 (an upstream such as Oura failing) always carries a problem document;
    /// the gateway's never does. So a gateway status with no problem document means the same as no
    /// answer at all: the server is not there, and every offline path should take it that way.
    static func isGatewayStandIn(status: Int, body: Data) -> Bool {
        isGatewayStandIn(status: status, problem: try? Wire.decoder().decode(ProblemDocument.self, from: body))
    }

    private static func isGatewayStandIn(status: Int, problem: ProblemDocument?) -> Bool {
        [502, 503, 504].contains(status) && problem == nil
    }

    /// Runs `request` and returns its body and status, translating a transport failure into an
    /// `APIError` and leaving cancellation as `CancellationError`.
    ///
    /// Cancellation is separated here rather than at each call site because `URLSession` reports it
    /// as `URLError.cancelled` — an ordinary-looking transport error. Letting it through as one
    /// would turn every dismissed screen into a visible failure.
    static func send(_ request: URLRequest, on session: URLSession) async throws -> (Data, Int) {
        try await transported {
            let (data, response) = try await session.data(for: request)
            return (data, try status(of: response))
        }
    }

    /// How long a stream of events may go with nothing arriving before it is taken as lost. The
    /// server sends a heartbeat every fifteen seconds while the model works, so three missed beats
    /// is a connection that has gone, not a model that is slow.
    static let streamSilence: TimeInterval = 45

    /// Opens `request` and returns its body as it arrives, once the status and headers are in.
    /// Failures are translated as `send` translates them; a failure partway through the body is
    /// thrown by the bytes themselves, untranslated, since only the reader knows what it lost.
    static func open(_ request: URLRequest, on session: URLSession) async throws -> (URLSession.AsyncBytes, Int) {
        try await transported {
            let (bytes, response) = try await session.bytes(for: request)
            return (bytes, try status(of: response))
        }
    }

    /// A body read to its end, for a status that is not a success and so is a small problem document.
    static func collect(_ bytes: URLSession.AsyncBytes) async throws -> Data {
        try await transported {
            try await bytes.reduce(into: Data()) { $0.append($1) }
        }
    }

    private static func status(of response: URLResponse) throws -> Int {
        guard let http = response as? HTTPURLResponse else {
            throw APIError.transport("The response was not HTTP.")
        }
        return http.statusCode
    }

    private static func transported<T>(_ exchange: () async throws -> T) async throws -> T {
        do {
            return try await exchange()
        } catch let error as URLError where error.code == .cancelled {
            throw CancellationError()
        } catch is CancellationError {
            throw CancellationError()
        } catch let error as URLError {
            throw APIError.transport(error.localizedDescription)
        }
    }

    static func decode<T: Decodable>(_ type: T.Type, from body: Data) throws -> T {
        // A 204 and an empty 200 both arrive as no bytes. A caller that wants nothing asks for
        // `Empty`, which is satisfied by that; anything else genuinely needed a body.
        if body.isEmpty, let empty = Empty() as? T { return empty }

        do {
            return try Wire.decoder().decode(T.self, from: body)
        } catch {
            throw APIError.decoding(String(describing: error))
        }
    }
}

/// The response of a route that answers with nothing.
struct Empty: Decodable, Sendable, Equatable {
    init() {}
    init(from decoder: any Decoder) throws {}
}
