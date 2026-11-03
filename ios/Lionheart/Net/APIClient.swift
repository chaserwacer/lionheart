import Foundation

/// The one way this app talks to Lionheart.
///
/// Everything about an authenticated request that is not the route lives here: the origin, the
/// bearer token, the single renewal on a 401, the decode, and the translation of a status into an
/// `APIError`. Endpoint types in `Net/Endpoints/` therefore hold routes and nothing else, and no
/// screen ever sees a status code.
///
/// Two things about the server's availability live here too, because this is the one place every
/// exchange passes through: each is reported to `ServerReachability`, and a remembered read keeps
/// its answer in `ReadMemory` and gives the kept one back when the server cannot be reached.
///
/// `Sendable` on purpose, and it is what the Today screen's parallel loads depend on: several
/// `async let` requests through one client are only expressible if the client can cross actors.
/// Nothing stored here is mutable — the mutable parts are the `TokenProvider` actor, the
/// main-actor reachability and the memory's own lock.
final class APIClient: Sendable {
    let configuration: APIConfiguration
    private let session: URLSession
    private let tokens: TokenProvider
    private let memory: ReadMemory
    private let reachability: ServerReachability

    init(
        configuration: APIConfiguration,
        tokens: TokenProvider,
        memory: ReadMemory,
        reachability: ServerReachability,
        session: URLSession = .shared
    ) {
        self.configuration = configuration
        self.tokens = tokens
        self.memory = memory
        self.reachability = reachability
        self.session = session
    }

    // MARK: - Authenticated

    func get<Response: Decodable>(
        _ path: String, query: [URLQueryItem] = [], as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.get, path, query: query, body: nil)
    }

    /// A read whose answer is kept, and given back marked with its time when the server cannot be
    /// reached.
    ///
    /// Only a request that never got an answer falls back. A server that answered with a fault is
    /// reachable and has said something the screen should hear, so that failure is thrown as it
    /// would be from `get`. The kept bytes decode through the same lenient decoder as fresh ones,
    /// so an answer kept by an older build reads the way a fresh one would.
    ///
    /// It waits `HTTP.readPatience` for an answer rather than the system's minute, because a server
    /// that is not there does not refuse the connection, it leaves it hanging, and the screen has
    /// something to show in the meantime.
    func get<Response: Decodable & Sendable>(
        _ path: String,
        query: [URLQueryItem] = [],
        as type: Response.Type = Response.self,
        remembering key: ReadKey
    ) async throws -> Remembered<Response> {
        let account = memory.currentAccount()
        do {
            let body = try await authenticatedBody(
                .get, path, query: query, body: nil, timeout: HTTP.readPatience)
            let value = try HTTP.decode(Response.self, from: body)
            // Kept only once it has decoded, so a body this build cannot read never replaces one
            // it can.
            memory.remember(body, as: key, for: account, at: Date())
            return .current(value)
        } catch APIError.transport(let description) {
            guard let kept = memory.recall(key, for: account),
                  let value = try? HTTP.decode(Response.self, from: kept.body)
            else { throw APIError.transport(description) }
            return .recalled(value, asOf: kept.asOf)
        }
    }

    /// The answer last kept for `key`, without asking the server. For the one read the app cannot
    /// wait on at all; every screen asks the server first.
    func recall<Response: Decodable & Sendable>(
        _ key: ReadKey, as type: Response.Type = Response.self
    ) -> Remembered<Response>? {
        guard let kept = memory.recall(key, for: memory.currentAccount()),
              let value = try? HTTP.decode(Response.self, from: kept.body)
        else { return nil }
        return .recalled(value, asOf: kept.asOf)
    }

    func post<Response: Decodable>(
        _ path: String, body: some Encodable, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.post, path, query: [], body: try Wire.encoder().encode(body))
    }

    func post<Response: Decodable>(
        _ path: String, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.post, path, query: [], body: nil)
    }

    func put<Response: Decodable>(
        _ path: String, body: some Encodable, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.put, path, query: [], body: try Wire.encoder().encode(body))
    }

    func patch<Response: Decodable>(
        _ path: String, body: some Encodable, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.patch, path, query: [], body: try Wire.encoder().encode(body))
    }

    func delete(_ path: String) async throws {
        _ = try await authenticated(.delete, path, query: [], body: nil) as Empty
    }

    // MARK: - Unauthenticated

    /// For the Identity routes that establish a session in the first place — `/login` and
    /// `/register` — which have no token to attach and must not trigger a renewal.
    func unauthenticated<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, body: some Encodable, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await open(method, path, body: try Wire.encoder().encode(body))
    }

    func unauthenticated<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await open(method, path, body: nil)
    }

    // MARK: -

    private func authenticated<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?
    ) async throws -> Response {
        try HTTP.decode(Response.self, from: await authenticatedBody(method, path, query: query, body: body))
    }

    /// Sends the request with a bearer token, and on a 401 renews once and sends it again. Answers
    /// the body of a 2xx.
    ///
    /// Exactly one retry. A second 401 after a fresh token means the credential is not what is
    /// wrong, and retrying again would turn one rejected request into a loop. The renewal itself is
    /// coalesced inside `TokenProvider`, so a burst of 401s costs one refresh between them.
    private func authenticatedBody(
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?,
        timeout: TimeInterval? = nil
    ) async throws -> Data {
        guard let accessToken = try await unreachedIsReported({ try await tokens.accessToken() }) else {
            throw APIError.unauthenticated
        }

        let (data, status) = try await send(
            method, path, query: query, body: body, bearer: accessToken, timeout: timeout)
        if status != 401 {
            return try Self.success(data: data, status: status)
        }

        let renewed = try await unreachedIsReported { try await tokens.renewed(replacing: accessToken) }
        let (retried, retriedStatus) = try await send(
            method, path, query: query, body: body, bearer: renewed.accessToken, timeout: timeout)
        return try Self.success(data: retried, status: retriedStatus)
    }

    private func open<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, body: Data?
    ) async throws -> Response {
        let request = HTTP.request(method, path: path, baseURL: configuration.baseURL, body: body)
        let (data, status) = try await reported { try await HTTP.send(request, on: session) }
        return try HTTP.decode(Response.self, from: try Self.success(data: data, status: status))
    }

    private func send(
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?, bearer: String,
        timeout: TimeInterval?
    ) async throws -> (Data, Int) {
        var request = HTTP.request(method, path: path, query: query, baseURL: configuration.baseURL, body: body)
        if let timeout { request.timeoutInterval = timeout }
        request.setValue("Bearer \(bearer)", forHTTPHeaderField: "Authorization")
        return try await reported { try await HTTP.send(request, on: session) }
    }

    /// Runs one request and tells `ServerReachability` what it came to.
    ///
    /// A request that never completed says the server is not reachable, and so does the gateway
    /// answering in its place (`HTTP.isGatewayStandIn`); any answer of the server's own, a 500
    /// included, says it is. `HTTP.send` throws nothing else but cancellation, which says neither.
    private func reported(_ request: () async throws -> (Data, Int)) async throws -> (Data, Int) {
        do {
            let answer = try await request()
            await reachability.record(answered: !HTTP.isGatewayStandIn(status: answer.1, body: answer.0))
            return answer
        } catch let error as APIError {
            await reachability.record(answered: false)
            throw error
        }
    }

    /// Runs a token step and reports only a renewal that never reached the server.
    ///
    /// Its success says nothing either way, since a token still fresh is answered without a
    /// request; but a renewal that could not be sent is the first thing an app relaunched with no
    /// signal meets, and it is the same server being unreachable.
    private func unreachedIsReported<T>(_ step: () async throws -> T) async throws -> T {
        do {
            return try await step()
        } catch APIError.transport(let description) {
            await reachability.record(answered: false)
            throw APIError.transport(description)
        }
    }

    private static func success(data: Data, status: Int) throws -> Data {
        if let failure = HTTP.failure(status: status, body: data) { throw failure }
        return data
    }
}
