import Foundation
import Observation

/// Who is signed in, and the root the app shows because of it.
///
/// It is the one piece of state that decides which half of the app exists. Everything session-
/// scoped is reset through `signOut`, so a second athlete on the same device cannot see the first
/// one's screens — which is why the stores that hold fetched data register themselves here rather
/// than each view remembering to clear itself.
@Observable
@MainActor
final class AuthStore {
    /// Which root `RootView` draws.
    enum Session: Equatable, Sendable {
        /// Before the Keychain has been consulted. A case rather than defaulting to signed-out,
        /// so the sign-in form does not flash on every cold launch of a signed-in app.
        case undetermined
        case signedOut
        case signedIn
    }

    private(set) var session: Session = .undetermined
    private(set) var signIn: SignInState = .editing

    /// Whether the signed-in session survived being written to the Keychain.
    ///
    /// False means the app works until it is quit and then forgets who is signed in. That is worth
    /// saying out loud — it is the kind of failure that otherwise reads to the athlete as the app
    /// signing them out at random.
    private(set) var isSessionPersisted = true

    private let identity: IdentityAPI
    private let tokens: TokenProvider
    private let custody: AccountCustody
    private let now: @Sendable () -> Date

    /// Teardown for state that belongs to one signed-in session. Registered by the stores that
    /// hold it, because this type must not know what screens exist.
    private var sessionScopedTeardown: [@MainActor () -> Void] = []
    /// Teardown for work the device holds for one account and nowhere else has; see
    /// `AccountCustody`.
    private var accountScopedTeardown: [@MainActor () -> Void] = []
    private var signInHandlers: [@MainActor () -> Void] = []

    init(
        identity: IdentityAPI,
        tokens: TokenProvider,
        custody: AccountCustody = AccountCustody(),
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.identity = identity
        self.tokens = tokens
        self.custody = custody
        self.now = now
    }

    /// Hands `teardown` the job of emptying one store whenever the session ends, however it ends.
    func onSignOut(_ teardown: @escaping @MainActor () -> Void) {
        sessionScopedTeardown.append(teardown)
    }

    /// Hands `teardown` the job of emptying work the device holds for the account: run when the
    /// athlete signs out, and when a sign-in after a forced sign-out is somebody else.
    ///
    /// Not run when the server forces the sign-out. That work exists nowhere else, and the same
    /// athlete signing straight back in should find it waiting.
    func onAccountChange(_ teardown: @escaping @MainActor () -> Void) {
        accountScopedTeardown.append(teardown)
    }

    /// Hands `handler` whatever has to happen once an athlete has signed in.
    func onSignIn(_ handler: @escaping @MainActor () -> Void) {
        signInHandlers.append(handler)
    }

    /// Reads whatever the Keychain holds and settles the root. Called once, from the app's `.task`.
    ///
    /// A stored token is taken at face value and not verified against the server: the first real
    /// request renews it if it has expired, and a sign-in form shown to somebody who is in fact
    /// signed in is a worse failure than a screen that loads and then reports a problem.
    func restore() async {
        guard session == .undetermined else { return }

        // A refresh token the server has already rotated away is caught by the first request, not
        // here — and that path signs the athlete out through the same handler.
        await tokens.onCredentialsRejected { [weak self] in
            await self?.credentialsRejected()
        }

        session = (await tokens.hasTokens) ? .signedIn : .signedOut
    }

    func submitSignIn(email: String, password: String) async {
        guard Credentials.areSubmittable(email: email, password: password) else { return }

        signIn = .submitting
        do {
            let account = email.trimmingCharacters(in: .whitespacesAndNewlines)
            let issued = try await identity.signIn(email: account, password: password)
            isSessionPersisted = await tokens.adopt(issued.tokens(receivedAt: now()))
            // Work kept through a forced sign-out is this account's to pick up, and nobody else's.
            if !custody.isHeld(for: account) { tearDown(accountScopedTeardown) }
            custody.hold(for: account)
            signIn = .editing
            session = .signedIn
            for handler in signInHandlers { handler() }
        } catch is CancellationError {
            signIn = .editing
        } catch {
            signIn = .refused((try? SignInFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    func signOut() async {
        await tokens.discard()
        tearDown(sessionScopedTeardown)
        tearDown(accountScopedTeardown)
        custody.release()
        signIn = .editing
        isSessionPersisted = true
        session = .signedOut
    }

    /// Clears the refusal so the form stops showing it once the athlete edits what they typed.
    func resumeEditing() {
        guard signIn.failure != nil else { return }
        signIn = .editing
    }

    /// The server refused the refresh token. The screens go, as on any sign-out; the work held for
    /// the account stays, with its custody, for the next sign-in to settle.
    private func credentialsRejected() {
        tearDown(sessionScopedTeardown)
        signIn = .refused(.credentialsRejected)
        session = .signedOut
    }

    private func tearDown(_ teardowns: [@MainActor () -> Void]) {
        for teardown in teardowns { teardown() }
    }
}
