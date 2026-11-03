import Foundation

/// Movement names by id, so a set row is one lookup rather than a scan.
///
/// A missing id is a real case and not a programming error: a session can outlive the movement it
/// referenced, and the catalogue read can fail while the session read succeeds. Both answer with
/// the same honest placeholder rather than hiding the set.
struct MovementCatalogue: Equatable, Sendable {
    private let namesById: [UUID: MovementData]

    init(_ movements: [MovementData] = []) {
        // Duplicate ids keep the first rather than trapping. `Dictionary(uniqueKeysWithValues:)`
        // would crash the screen on a server that repeated one, which is not a failure the athlete
        // should wear for a catalogue read.
        namesById = Dictionary(
            movements.map { ($0.movementDataId, $0) }, uniquingKeysWith: { first, _ in first })
    }

    var isEmpty: Bool { namesById.isEmpty }

    func movement(_ id: UUID) -> MovementData? { namesById[id] }

    /// The ids among `ids` this catalogue has no movement for — what a screen holding them has to
    /// ask the server about before it can name them.
    func missing(_ ids: some Sequence<UUID>) -> Set<UUID> {
        Set(ids.filter { namesById[$0] == nil })
    }

    /// What to print for a set's movement. `nil` is a set shape with no movement identity at all —
    /// a trail run, a freeform log — which is a different thing from a movement this catalogue
    /// could not resolve.
    func name(for id: UUID?) -> String {
        guard let id else { return "Unspecified" }
        return namesById[id]?.displayName ?? "Unknown movement"
    }
}
