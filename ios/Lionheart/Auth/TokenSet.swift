import Foundation

/// The bearer credentials ASP.NET Identity issues.
///
/// `POST /login` and `POST /refresh` both answer with all three members, so a renewal replaces the
/// refresh token as well as the access token — keeping the old one is how a client ends up with a
/// refresh token the server has already rotated away.
///
/// `expiresAt` is derived at the moment of receipt from the `expiresIn` the server sent, because a
/// relative lifetime stops meaning anything once it has been written to disk.
struct TokenSet: Equatable, Sendable, Codable {
    let accessToken: String
    let refreshToken: String
    let expiresAt: Date

    init(accessToken: String, refreshToken: String, expiresAt: Date) {
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        self.expiresAt = expiresAt
    }

    /// Whether the access token is close enough to expiry to renew before using it.
    ///
    /// The margin exists because expiry is checked on the device and spent on the server: a token
    /// with two seconds left will be expired by the time the request lands.
    func isFresh(at now: Date, margin: TimeInterval = 30) -> Bool {
        expiresAt.timeIntervalSince(now) > margin
    }
}

/// What `POST /login` and `POST /refresh` answer with.
struct IdentityTokenResponse: Equatable, Sendable, Decodable {
    let accessToken: String
    let refreshToken: String
    let expiresIn: TimeInterval

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let accessToken = container.lenient(String.self, "accessToken"),
              let refreshToken = container.lenient(String.self, "refreshToken")
        else {
            throw APIError.decoding("The sign-in response carried no tokens.")
        }
        self.accessToken = accessToken
        self.refreshToken = refreshToken
        expiresIn = container.lenient("expiresIn", or: 3600)
    }

    func tokens(receivedAt now: Date) -> TokenSet {
        TokenSet(
            accessToken: accessToken,
            refreshToken: refreshToken,
            expiresAt: now.addingTimeInterval(expiresIn))
    }
}
