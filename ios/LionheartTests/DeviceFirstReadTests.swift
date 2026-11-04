import Foundation
import Testing

@testable import Lionheart

/// A remembered read answered from the device alone, and the count of writes a screen judges its
/// answer by — run against the real client over a stubbed server, because both claims are about
/// which requests are made.
@MainActor
@Suite("Device-first reads")
struct DeviceFirstReadTests {
    @MainActor
    private final class Rig {
        let host = "\(UUID().uuidString.lowercased()).test"
        let client: APIClient
        let tokens: TokenProvider

        init() async {
            ACatalogueServer.serve(host: host)
            let configuration = APIConfiguration(baseURL: URL(string: "https://\(host)")!)
            let session = ACatalogueServer.session()
            let tokens = TokenProvider(
                configuration: configuration,
                session: session,
                keychain: KeychainTokenStore(service: "lionheart.tests.\(host)"))
            client = APIClient(
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
            ACatalogueServer.list([stubbedMovements[0].id], on: host)
        }

        var reads: Int { ACatalogueServer.reads(on: host) }
        var movements: MovementsAPI { MovementsAPI(client: client) }
    }

    @Test("The device answers with what it kept, marked with its time, and asks the server nothing")
    func theDeviceAnswersAlone() async throws {
        let rig = await Rig()
        let fromServer = try await rig.movements.movements(remembering: .movements)
        #expect(rig.reads == 1)

        let movements = rig.movements
        let fromDevice = try #require(await APIClient.onDevice {
            try await movements.movements(remembering: .movements)
        })

        #expect(rig.reads == 1)
        #expect(fromDevice.asOf != nil)
        #expect(fromDevice.value.map(\.movementDataId) == fromServer.value.map(\.movementDataId))
        await rig.tokens.discard()
    }

    @Test("With nothing kept the device has no answer, and still asks the server nothing")
    func nothingKeptIsNoAnswer() async {
        let rig = await Rig()
        let movements = rig.movements

        let fromDevice = await APIClient.onDevice { try await movements.movements(remembering: .movements) }

        #expect(fromDevice == nil)
        #expect(rig.reads == 0)
        await rig.tokens.discard()
    }

    @Test("Only a write the server accepted is counted; reads are not")
    func writesAreCounted() async throws {
        let rig = await Rig()
        let before = rig.client.writes.accepted

        _ = try await rig.movements.movements(remembering: .movements)
        #expect(rig.client.writes.accepted == before)

        try await rig.client.delete("/api/anything")
        #expect(rig.client.writes.accepted == before + 1)
        await rig.tokens.discard()
    }
}
