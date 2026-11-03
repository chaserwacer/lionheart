import Foundation

/// The one way this app talks to Lionheart.
///
/// Everything about an authenticated request that is not the route lives here: the origin, the
/// bearer token, the single renewal on a 401, the decode, and the translation of a status into an
/// `APIError`. Endpoint types in `Net/Endpoints/` therefore hold routes and nothing else, and no
/// screen ever sees a status code.
///
/// `Sendable` on purpose, and it is what the Today screen's parallel loads depend on: several
/// `async let` requests through one client are only expressible if the client can cross actors.
/// Nothing stored here is mutable — the mutable part is the `TokenProvider` actor.
final class APIClient: Sendable {
    let configuration: APIConfiguration
    private let session: URLSession
    private let tokens: TokenProvider

    init(configuration: APIConfiguration, tokens: TokenProvider, session: URLSession = .shared) {
        self.configuration = configuration
        self.tokens = tokens
        self.session = session
    }

    // MARK: - Authenticated

    func get<Response: Decodable>(
        _ path: String, query: [URLQueryItem] = [], as type: Response.Type = Response.self
    ) async throws -> Response {
        try await authenticated(.get, path, query: query, body: nil)
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

    /// Sends the request with a bearer token, and on a 401 renews once and sends it again.
    ///
    /// Exactly one retry. A second 401 after a fresh token means the credential is not what is
    /// wrong, and retrying again would turn one rejected request into a loop. The renewal itself is
    /// coalesced inside `TokenProvider`, so a burst of 401s costs one refresh between them.
    private func authenticated<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?
    ) async throws -> Response {
        guard let accessToken = try await tokens.accessToken() else { throw APIError.unauthenticated }

        let (data, status) = try await send(method, path, query: query, body: body, bearer: accessToken)
        if status != 401 {
            return try outcome(Response.self, data: data, status: status)
        }

        let renewed = try await tokens.renewed(replacing: accessToken)
        let (retried, retriedStatus) = try await send(
            method, path, query: query, body: body, bearer: renewed.accessToken)
        return try outcome(Response.self, data: retried, status: retriedStatus)
    }

    private func open<Response: Decodable>(
        _ method: HTTP.Method, _ path: String, body: Data?
    ) async throws -> Response {
        let request = HTTP.request(method, path: path, baseURL: configuration.baseURL, body: body)
        let (data, status) = try await HTTP.send(request, on: session)
        return try outcome(Response.self, data: data, status: status)
    }

    private func send(
        _ method: HTTP.Method, _ path: String, query: [URLQueryItem], body: Data?, bearer: String
    ) async throws -> (Data, Int) {
        var request = HTTP.request(method, path: path, query: query, baseURL: configuration.baseURL, body: body)
        request.setValue("Bearer \(bearer)", forHTTPHeaderField: "Authorization")
        return try await HTTP.send(request, on: session)
    }

    private func outcome<Response: Decodable>(
        _ type: Response.Type, data: Data, status: Int
    ) throws -> Response {
        if let failure = HTTP.failure(status: status, body: data) { throw failure }
        return try HTTP.decode(Response.self, from: data)
    }
}
