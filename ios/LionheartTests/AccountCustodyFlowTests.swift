import Foundation
import Synchronization
import Testing

@testable import Lionheart

/// What becomes of the work held on the device across the ways a session can end.
///
/// Run against `AuthStore` with its real token provider and client, over a stubbed server: the
/// claim is about which teardown each kind of sign-out runs, and that is decided by the order the
/// real pieces call each other in.
@MainActor
@Suite("Account custody across sign-outs")
struct AccountCustodyFlowTests {
    /// The pieces a test drives, and the teardowns it counts.
    @MainActor
    private final class Rig {
        let auth: AuthStore
        let client: APIClient
        let tokens: TokenProvider
        let clock: AClock
        var sessionWipes = 0
        var accountWipes = 0

        init() async {
            let host = "\(UUID().uuidString.lowercased()).test"
            AStubbedServer.serve(host: host)
            let configuration = APIConfiguration(baseURL: URL(string: "https://\(host)")!)
            let session = AStubbedServer.session()
            let temporary = URL.temporaryDirectory.appending(path: UUID().uuidString)
            let clock = AClock()
            let tokens = TokenProvider(
                configuration: configuration,
                session: session,
                keychain: KeychainTokenStore(service: "lionheart.tests.\(host)"),
                now: { clock.now })
            let client = APIClient(
                configuration: configuration,
                tokens: tokens,
                memory: ReadMemory(root: temporary.appending(path: "remembered")),
                reachability: ServerReachability(),
                session: session)
            self.auth = AuthStore(
                identity: IdentityAPI(client: client),
                tokens: tokens,
                custody: AccountCustody(directoryURL: temporary),
                now: { clock.now })
            self.client = client
            self.tokens = tokens
            self.clock = clock
            auth.onSignOut { [unowned self] in sessionWipes += 1 }
            auth.onAccountChange { [unowned self] in accountWipes += 1 }
            await auth.restore()
        }

        func signIn(_ email: String) async {
            await auth.submitSignIn(email: email, password: "a password")
        }

        /// The access token lapses and the server refuses the refresh token, which is how a
        /// sign-out is forced on the athlete.
        func forceSignOut() async {
            clock.advance(by: 7_200)
            _ = try? await client.get("/api/anything", as: Empty.self)
        }

        func tearDown() async {
            await tokens.discard()
        }
    }

    @Test("A sign-out the server forces keeps the account's work for it")
    func aForcedSignOutKeepsTheWork() async {
        let rig = await Rig()
        await rig.signIn("chase@example.com")
        let wipesAtSignIn = rig.accountWipes

        await rig.forceSignOut()

        #expect(rig.auth.session == .signedOut)
        #expect(rig.sessionWipes == 1)
        #expect(rig.accountWipes == wipesAtSignIn)

        // The same athlete, however they type the address, finds it waiting.
        await rig.signIn(" Chase@Example.com ")
        #expect(rig.auth.session == .signedIn)
        #expect(rig.accountWipes == wipesAtSignIn)
        await rig.tearDown()
    }

    @Test("Somebody else signing in after a forced sign-out empties the work")
    func anotherAccountEmptiesTheWork() async {
        let rig = await Rig()
        await rig.signIn("chase@example.com")
        let wipesAtSignIn = rig.accountWipes
        await rig.forceSignOut()

        await rig.signIn("someone@example.com")

        #expect(rig.auth.session == .signedIn)
        #expect(rig.accountWipes == wipesAtSignIn + 1)
        await rig.tearDown()
    }

    @Test("Signing out by choice empties the work, and holds nothing for the next sign-in")
    func aChosenSignOutEmptiesTheWork() async {
        let rig = await Rig()
        await rig.signIn("chase@example.com")
        let wipesAtSignIn = rig.accountWipes

        await rig.auth.signOut()
        #expect(rig.accountWipes == wipesAtSignIn + 1)

        // Custody went with it, so even the same account starts from nothing.
        await rig.signIn("chase@example.com")
        #expect(rig.accountWipes == wipesAtSignIn + 2)
        await rig.tearDown()
    }
}

/// A clock a test moves by hand.
final class AClock: Sendable {
    private let instant = Mutex(Date(timeIntervalSinceReferenceDate: 800_000_000))

    var now: Date { instant.withLock { $0 } }

    func advance(by interval: TimeInterval) {
        instant.withLock { $0 = $0.addingTimeInterval(interval) }
    }
}

/// Identity's routes as a test needs them: sign-in issues a token, renewal refuses it, and
/// anything else answers empty. Keyed by host so suites running at once do not share a server.
///
/// `@unchecked Sendable` because `URLProtocol` is an Objective-C class the compiler cannot check;
/// the only shared state is `hosts`, behind its `Mutex`.
final class AStubbedServer: URLProtocol, @unchecked Sendable {
    private static let hosts = Mutex<Set<String>>([])

    static func serve(host: String) {
        _ = hosts.withLock { $0.insert(host) }
    }

    static func session() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [AStubbedServer.self]
        return URLSession(configuration: configuration)
    }

    override class func canInit(with request: URLRequest) -> Bool {
        guard let host = request.url?.host() else { return false }
        return hosts.withLock { $0.contains(host) }
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url else { return }
        let (status, body): (Int, String) = switch url.path() {
        case "/login": (200, #"{"accessToken":"issued","refreshToken":"renewable","expiresIn":3600}"#)
        case "/refresh": (401, "")
        default: (200, "")
        }
        let response = HTTPURLResponse(url: url, statusCode: status, httpVersion: "HTTP/1.1", headerFields: nil)!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
