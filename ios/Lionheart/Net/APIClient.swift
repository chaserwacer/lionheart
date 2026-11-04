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
/// Every write the server accepts is counted in the `WriteLedger`, which is how a screen knows its
/// answer may be out of date.
///
/// `Sendable` on purpose, and it is what the Today screen's parallel loads depend on: several
/// `async let` requests through one client are only expressible if the client can cross actors.
/// Nothing stored here is mutable — the mutable parts are the `TokenProvider` actor, the
/// main-actor reachability, and the memory's and the ledger's own locks.
final class APIClient: Sendable {
    let configuration: APIConfiguration
    let writes: WriteLedger
    private let session: URLSession
    private let tokens: TokenProvider
    private let memory: ReadMemory
    private let reachability: ServerReachability

    /// Whether remembered reads in the current task answer from the device alone.
    ///
    /// Set for the length of one read with `onDevice(_:)`, which is how a store paints a screen it
    /// has shown before from the device's last answers before the server has said anything. A task
    /// value rather than a parameter because the read it scopes is a store's whole reading of its
    /// screen — the same endpoints, windows and keys, through `async let`s whose child tasks
    /// inherit it — and threading a flag through every endpoint method would give each of them a
    /// second way to be called that only one caller uses.
    @TaskLocal private static var answersFromDevice = false

    init(
        configuration: APIConfiguration,
        tokens: TokenProvider,
        memory: ReadMemory,
        reachability: ServerReachability,
        writes: WriteLedger = WriteLedger(),
        session: URLSession = .shared
    ) {
        self.configuration = configuration
        self.tokens = tokens
        self.memory = memory
        self.reachability = reachability
        self.writes = writes
        self.session = session
    }

    /// Runs `read` with every remembered read in it answered from the device, without a request.
    ///
    /// A read with nothing kept throws, so `read` as a whole answers only when the device holds
    /// every part of it; the answer is `nil` otherwise. Nothing here reaches the server or tells
    /// `ServerReachability` anything.
    static func onDevice<Value: Sendable>(
        isolation: isolated (any Actor)? = #isolation,
        _ read: () async throws -> Value
    ) async -> Value? {
        try? await $answersFromDevice.withValue(true) { try await read() }
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
        if Self.answersFromDevice {
            guard let kept = memory.recall(key, for: account) else {
                throw APIError.transport("Nothing is kept on this device for \(key.name).")
            }
            return .recalled(try HTTP.decode(Response.self, from: kept.body), asOf: kept.asOf)
        }
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

    /// - Parameter timeout: How long to wait with nothing back, when the system's minute is wrong for
    ///   the route; see `HTTP.modelPatience`.
    func post<Response: Decodable>(
        _ path: String, body: some Encodable, timeout: TimeInterval? = nil, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.post, path, query: [], body: try Wire.encoder().encode(body), timeout: timeout)
    }

    func post<Response: Decodable>(
        _ path: String, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.post, path, query: [], body: nil)
    }

    /// A POST answered with server-sent events, read as they arrive.
    ///
    /// Returns once the server has accepted the request, with every event still to come. Up to that
    /// point it fails as `post` does — renewing once on a 401, throwing the server's refusal as an
    /// `APIError` — so a throw here means the server took nothing on. A failure after it is the
    /// events' own, thrown while they are read.
    func postReceivingEvents(
        _ path: String, body: some Encodable
    ) async throws -> ServerSentEvents<AsyncLineSequence<URLSession.AsyncBytes>> {
        let encoded = try Wire.encoder().encode(body)
        guard let accessToken = try await unreachedIsReported({ try await tokens.accessToken() }) else {
            throw APIError.unauthenticated
        }

        var (bytes, status) = try await openEvents(path, body: encoded, bearer: accessToken)
        if status == 401 {
            bytes.task.cancel()
            let renewed = try await unreachedIsReported { try await tokens.renewed(replacing: accessToken) }
            (bytes, status) = try await openEvents(path, body: encoded, bearer: renewed.accessToken)
        }

        guard (200..<300).contains(status) else {
            let problem = try await HTTP.collect(bytes)
            await reachability.record(answered: !HTTP.isGatewayStandIn(status: status, body: problem))
            throw HTTP.failure(status: status, body: problem) ?? APIError.http(status: status, detail: nil)
        }
        await reachability.record(answered: true)
        writes.recordAccepted()
        return ServerSentEvents(lines: bytes.lines)
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

    /// A delete the server answers with the record as it now stands, rather than with nothing.
    func deleteAnswering<Response: Decodable>(
        _ path: String, as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.delete, path, query: [], body: nil)
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
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?,
        timeout: TimeInterval? = nil
    ) async throws -> Response {
        let answer = try await authenticatedBody(method, path, query: query, body: body, timeout: timeout)
        if method != .get { writes.recordAccepted() }
        return try HTTP.decode(Response.self, from: answer)
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

    private func openEvents(
        _ path: String, body: Data, bearer: String
    ) async throws -> (URLSession.AsyncBytes, Int) {
        var request = HTTP.request(.post, path: path, baseURL: configuration.baseURL, body: body)
        request.timeoutInterval = HTTP.streamSilence
        request.setValue("text/event-stream", forHTTPHeaderField: "Accept")
        request.setValue("Bearer \(bearer)", forHTTPHeaderField: "Authorization")
        do {
            return try await HTTP.open(request, on: session)
        } catch let error as APIError {
            await reachability.record(answered: false)
            throw error
        }
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
