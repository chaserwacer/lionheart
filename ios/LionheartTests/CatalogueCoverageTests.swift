import Foundation
import Synchronization
import Testing

@testable import Lionheart

/// A movement the catalogue does not hold is read for once, not shown as unknown and not asked
/// about on every render.
///
/// Run against the real library store, client and token provider over a stubbed server, because the
/// claim is about how many times the catalogue is read.
@MainActor
@Suite("Catalogue coverage")
struct CatalogueCoverageTests {
    private static let squat = stubbedMovements[0].id
    private static let dip = stubbedMovements[1].id
    private static let gone = UUID(uuidString: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee")!

    @MainActor
    private final class Rig {
        let host = "\(UUID().uuidString.lowercased()).test"
        let library: MovementLibraryStore
        let tokens: TokenProvider

        init() async {
            ACatalogueServer.serve(host: host)
            let configuration = APIConfiguration(baseURL: URL(string: "https://\(host)")!)
            let session = ACatalogueServer.session()
            let tokens = TokenProvider(
                configuration: configuration,
                session: session,
                keychain: KeychainTokenStore(service: "lionheart.tests.\(host)"))
            let client = APIClient(
                configuration: configuration,
                tokens: tokens,
                memory: ReadMemory(root: URL.temporaryDirectory.appending(path: UUID().uuidString)),
                reachability: ServerReachability(),
                session: session)
            let auth = AuthStore(
                identity: IdentityAPI(client: client), tokens: tokens,
                custody: AccountCustody(directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString)))
            await auth.restore()
            await auth.submitSignIn(email: "chase@example.com", password: "a password")
            self.tokens = tokens
            library = MovementLibraryStore(
                movements: MovementsAPI(client: client),
                records: PersonalRecordsAPI(client: client),
                defaults: UserDefaults(suiteName: host)!)
        }

        var reads: Int { ACatalogueServer.reads(on: host) }

        func list(_ ids: UUID...) { ACatalogueServer.list(ids, on: host) }
    }

    @Test("A movement made since the catalogue loaded is read for once, and then named")
    func aNewMovementIsReadFor() async {
        let rig = await Rig()
        rig.list(Self.squat)
        await rig.library.refresh()
        #expect(rig.reads == 1)

        rig.list(Self.squat, Self.dip)
        await rig.library.cover([Self.squat, Self.dip])

        #expect(rig.reads == 2)
        #expect(rig.library.title(of: Self.dip) == "Dip")
        await rig.tokens.discard()
    }

    @Test("A catalogue that already names every movement is not read again")
    func nothingMissingReadsNothing() async {
        let rig = await Rig()
        rig.list(Self.squat, Self.dip)
        await rig.library.refresh()

        await rig.library.cover([Self.squat, Self.dip])

        #expect(rig.reads == 1)
        await rig.tokens.discard()
    }

    @Test("A movement the server does not list costs one read, not one per ask")
    func anUnlistedMovementIsAskedAboutOnce() async {
        let rig = await Rig()
        rig.list(Self.squat)
        await rig.library.refresh()

        await rig.library.cover([Self.gone])
        await rig.library.cover([Self.gone])

        #expect(rig.reads == 2)
        #expect(rig.library.title(of: Self.gone) == "Unknown movement")
        await rig.tokens.discard()
    }
}

/// Identity's sign-in and a movement catalogue a test sets, counting how often the catalogue is
/// read. Keyed by host so suites running at once do not share a server.
///
/// `@unchecked Sendable` because `URLProtocol` is an Objective-C class the compiler cannot check;
/// the shared state is behind its `Mutex`.
final class ACatalogueServer: URLProtocol, @unchecked Sendable {
    private struct Host {
        var movements: [UUID] = []
        var reads = 0
    }

    private static let hosts = Mutex<[String: Host]>([:])

    static func serve(host: String) {
        hosts.withLock { $0[host] = Host() }
    }

    static func list(_ ids: [UUID], on host: String) {
        hosts.withLock { $0[host]?.movements = ids }
    }

    static func reads(on host: String) -> Int {
        hosts.withLock { $0[host]?.reads ?? 0 }
    }

    static func session() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [ACatalogueServer.self]
        return URLSession(configuration: configuration)
    }

    override class func canInit(with request: URLRequest) -> Bool {
        guard let host = request.url?.host() else { return false }
        return hosts.withLock { $0[host] != nil }
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let url = request.url, let host = url.host() else { return }
        let body: String = switch url.path() {
        case "/login":
            #"{"accessToken":"issued","refreshToken":"renewable","expiresIn":3600}"#
        case "/api/movement-datas":
            Self.hosts.withLock { hosts -> String in
                hosts[host]?.reads += 1
                let listed = (hosts[host]?.movements ?? []).compactMap { id in
                    stubbedMovements.first { $0.id == id }.map { Catalogue.movement(id: id.uuidString, base: $0.name) }
                }
                return "[\(listed.joined(separator: ","))]"
            }
        case "/api/personal-records/summaries":
            "[]"
        default:
            ""
        }
        let response = HTTPURLResponse(url: url, statusCode: 200, httpVersion: "HTTP/1.1", headerFields: nil)!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data(body.utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

/// The movements the stub server can list, and what it names them.
private let stubbedMovements: [(id: UUID, name: String)] = [
    (UUID(uuidString: "cccccccc-cccc-cccc-cccc-cccccccccccc")!, "Squat"),
    (UUID(uuidString: "dddddddd-dddd-dddd-dddd-dddddddddddd")!, "Dip"),
]
