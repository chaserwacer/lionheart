import Foundation

/// The movement catalogue — what the exercise library reads.
///
/// The one write is the athlete's own setup for a movement — notes and how its weight is read —
/// which is part of training with it. Creating a movement needs an equipment and a movement base chosen or
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

    /// Replaces the athlete's setup for a movement — the notes shown while logging it, and whether
    /// its weight is added to bodyweight. Answers the movement as it now stands.
    func updateSetup(_ setup: MovementSetup, of movementDataId: UUID) async throws -> MovementData {
        try await client.put(
            "/api/movement-datas/\(movementDataId.uuidString)/setup",
            body: MovementSetupBody(notes: setup.notes, addsToBodyweight: setup.addsToBodyweight))
    }
}

extension MovementsAPI {
    /// States what a movement base trains, for every movement built on it. Answers the base's
    /// classification as it now stands.
    func classify(base movementBaseId: UUID, as classification: MovementClassification) async throws -> ClassificationStanding {
        let answered: MovementBaseAnswer = try await client.put(
            "/api/movement-bases/\(movementBaseId.uuidString)/classification",
            body: ClassificationBody(
                pattern: classification.pattern.wire,
                primary: classification.primary.map(\.wire),
                secondary: classification.secondary.map(\.wire)))
        return answered.classification
    }

    /// Withdraws the athlete's classification, so the shipped map answers for the base again.
    func withdrawClassification(base movementBaseId: UUID) async throws -> ClassificationStanding {
        let answered: MovementBaseAnswer = try await client.deleteAnswering(
            "/api/movement-bases/\(movementBaseId.uuidString)/classification")
        return answered.classification
    }
}

private struct ClassificationBody: Encodable, Sendable {
    let pattern: String
    let primary: [String]
    let secondary: [String]
}

/// A `MovementBaseDTO`, of which only the classification is read back.
private struct MovementBaseAnswer: Decodable, Sendable {
    let classification: ClassificationStanding

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        classification = container.lenient(ClassificationStanding.self, "classification") ?? .unrecognised("")
    }
}

private struct MovementSetupBody: Encodable, Sendable {
    let notes: String
    let addsToBodyweight: Bool
}
