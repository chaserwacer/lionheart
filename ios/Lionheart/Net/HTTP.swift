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

    /// Runs `request` and returns its body and status, translating a transport failure into an
    /// `APIError` and leaving cancellation as `CancellationError`.
    ///
    /// Cancellation is separated here rather than at each call site because `URLSession` reports it
    /// as `URLError.cancelled` — an ordinary-looking transport error. Letting it through as one
    /// would turn every dismissed screen into a visible failure.
    static func send(_ request: URLRequest, on session: URLSession) async throws -> (Data, Int) {
        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                throw APIError.transport("The response was not HTTP.")
            }
            return (data, http.statusCode)
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
