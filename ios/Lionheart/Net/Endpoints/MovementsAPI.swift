import Foundation

/// The movement catalogue — what the exercise library reads.
///
/// Read-only on purpose. Creating a movement needs an equipment and a movement base chosen or
/// invented first, which is catalogue curation rather than training, and the web app already does
/// it; a second path to the same place is surface nobody asked for. A library with nothing in it
/// therefore says so and points at the web app rather than offering a form.
///
/// These routes predate the data remodel, which shows in two places a reader should not mistake for
/// mistakes: the paths are verb-ish (`/get-all`) rather than resourceful, and their payloads spell
/// identifiers `…ID` where the remodelled types spell them `…Id`.
struct MovementsAPI: Sendable {
    let client: APIClient

    /// Every movement the athlete has configured, remembered under `key`. The picker needs it most
    /// with no signal: without it a workout started in a basement has nothing to log against.
    func movements(remembering key: ReadKey) async throws -> Remembered<[MovementData]> {
        try await client.get(
            "/api/movement-datas", as: LenientList<MovementData>.self, remembering: key
        ).map(\.elements)
    }
}
