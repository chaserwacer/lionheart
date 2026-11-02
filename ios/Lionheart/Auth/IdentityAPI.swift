import Foundation

/// ASP.NET Identity's own routes, which the server maps at the root rather than under `/api`.
///
/// `POST /login` is sent **without** `useCookies`, which is what makes Identity answer with
/// `accessToken`/`refreshToken`/`expiresIn` instead of setting a cookie. Sending
/// `useCookies=true` would succeed and return a body with no tokens in it, which is the one
/// mistake here worth naming.
///
/// Renewal is not on this type: `POST /refresh` belongs to `TokenProvider`, which has to be the
/// only thing that calls it in order to coalesce a burst into one request.
struct IdentityAPI: Sendable {
    let client: APIClient

    func signIn(email: String, password: String) async throws -> IdentityTokenResponse {
        try await client.unauthenticated(
            .post, "/login", body: Credentials(email: email, password: password))
    }

    /// Creates an account. Identity answers 200 with an empty body, and a weak or duplicate
    /// credential comes back as a 400 whose `errors` name the problem.
    func register(email: String, password: String) async throws {
        _ = try await client.unauthenticated(
            .post, "/register", body: Credentials(email: email, password: password), as: Empty.self)
    }

    private struct Credentials: Encodable, Sendable {
        let email: String
        let password: String
    }
}
