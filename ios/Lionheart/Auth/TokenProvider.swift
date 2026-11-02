import Foundation

/// The single owner of the signed-in athlete's tokens, and the only thing that renews them.
///
/// An actor because renewal must not happen twice. Four parallel loads on the Today screen all
/// reach a server that has just expired their access token, all get a 401, and all ask for a
/// renewal: a second refresh with a refresh token the first one has already rotated away would
/// fail, and the athlete would be signed out in the middle of a working session. Serialising the
/// decision here means the first caller starts one renewal, the other three await the same task,
/// and a burst of 401s produces exactly one request.
///
/// It performs that one request itself through `HTTP` rather than calling back into `APIClient`,
/// which keeps the dependency one-way: the client reaches the token provider, never the reverse.
actor TokenProvider {
    private let configuration: APIConfiguration
    private let session: URLSession
    private let keychain: KeychainTokenStore
    private let now: @Sendable () -> Date

    private var tokens: TokenSet?
    private var renewal: Task<TokenSet, any Error>?

    /// Called when renewal has failed in a way that cannot be retried, so the app can go back to
    /// its signed-out root. Nothing is torn down from inside here; that is `AuthStore`'s job.
    private var onCredentialsRejected: (@Sendable () async -> Void)?

    init(
        configuration: APIConfiguration,
        session: URLSession = .shared,
        keychain: KeychainTokenStore = KeychainTokenStore(),
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.configuration = configuration
        self.session = session
        self.keychain = keychain
        self.now = now
        tokens = keychain.load()
    }

    var hasTokens: Bool { tokens != nil }

    func onCredentialsRejected(_ handler: @escaping @Sendable () async -> Void) {
        onCredentialsRejected = handler
    }

    /// Adopts a freshly issued set — what sign-in calls.
    ///
    /// - Returns: whether the Keychain kept them. A session works either way for as long as the app
    ///   is running, so a failure is not a reason to refuse the sign-in — but it means the athlete
    ///   will be signed out on the next launch, and being told that beats discovering it.
    @discardableResult
    func adopt(_ newTokens: TokenSet) -> Bool {
        renewal?.cancel()
        renewal = nil
        tokens = newTokens
        return keychain.save(newTokens)
    }

    func discard() {
        renewal?.cancel()
        renewal = nil
        tokens = nil
        keychain.clear()
    }

    /// An access token to send, renewing first if the one held is at or past expiry.
    ///
    /// Returns `nil` only when there is nothing to renew from — nobody is signed in. A renewal
    /// that fails transiently throws, so the caller can report it rather than mistake it for
    /// being signed out.
    func accessToken() async throws -> String? {
        guard let held = tokens else { return nil }
        guard !held.isFresh(at: now()) else { return held.accessToken }
        return try await renewed(replacing: held.accessToken).accessToken
    }

    /// Renews the set, unless `staleAccessToken` is already not the one held — in which case
    /// somebody else's renewal has landed and theirs is the answer.
    ///
    /// This is what a 401 retry calls. The staleness check is what makes a burst idempotent: the
    /// second caller through the door finds a different token than the one its request failed with
    /// and takes it, without a second request.
    func renewed(replacing staleAccessToken: String) async throws -> TokenSet {
        guard let held = tokens else { throw APIError.unauthenticated }
        if held.accessToken != staleAccessToken { return held }

        // Whoever arrives first starts the one renewal; everyone else awaits the same task. The
        // handle is stored rather than the work being repeated, which is the whole point.
        if renewal == nil {
            renewal = Task { [self] in try await requestRenewal(using: held.refreshToken) }
        }
        guard let task = renewal else { throw APIError.unauthenticated }

        defer { renewal = nil }

        do {
            let renewed = try await task.value
            tokens = renewed
            return renewed
        } catch is CancellationError {
            throw CancellationError()
        } catch let error as APIError where error.rejectsCredential {
            // The refresh token itself was refused. Nothing the athlete can retry, so the app goes
            // back to its signed-out root rather than holding a credential the server has dropped.
            await rejectCredentials()
            throw APIError.unauthenticated
        }
    }

    /// The renewal request itself. `nonisolated` so it runs off the actor: the actor's job is
    /// deciding that exactly one of these happens, not holding its own queue for the duration of
    /// a network round trip.
    private nonisolated func requestRenewal(using refreshToken: String) async throws -> TokenSet {
        let body = try JSONEncoder().encode(["refreshToken": refreshToken])
        let request = HTTP.request(.post, path: "/refresh", baseURL: configuration.baseURL, body: body)
        let (data, status) = try await HTTP.send(request, on: session)

        if let failure = HTTP.failure(status: status, body: data) { throw failure }

        let renewed = try HTTP.decode(IdentityTokenResponse.self, from: data).tokens(receivedAt: now())
        keychain.save(renewed)
        return renewed
    }

    private func rejectCredentials() async {
        tokens = nil
        keychain.clear()
        await onCredentialsRejected?()
    }
}
