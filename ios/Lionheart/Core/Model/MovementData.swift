import Foundation

/// The identity of a thing you do: a movement base, the equipment it is done with, and an optional
/// modifier.
///
/// A set entry carries only a `movementDataId`, so every screen that shows a set needs this
/// catalogue to put a name on it. It is fetched once per screen load and held as a lookup rather
/// than joined per row.
struct MovementData: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let baseName: String
    let equipmentName: String
    /// "Incline", "Paused", "Wide grip" — absent for the plain movement.
    let modifierName: String?

    var id: UUID { movementDataId }

    /// The movement as a reader names it: modifier, equipment, base, in the order a lifter says
    /// them. Equipment is dropped when the base already contains the word, because "Barbell
    /// Barbell Row" is how a naive join reads.
    var displayName: String {
        let base = baseName.isEmpty ? "Unnamed movement" : baseName
        var words: [String] = []
        if let modifierName, !modifierName.isEmpty { words.append(modifierName) }
        if !equipmentName.isEmpty, !base.localizedCaseInsensitiveContains(equipmentName) {
            words.append(equipmentName)
        }
        words.append(base)
        return words.joined(separator: " ")
    }
}

extension MovementData: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let movementDataId = container.lenient(UUID.self, "movementDataID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A movement without an id cannot name a set."))
        }

        self.init(
            movementDataId: movementDataId,
            baseName: Self.name(in: container, "movementBase"),
            equipmentName: Self.name(in: container, "equipment"),
            modifierName: Self.name(in: container, "modifier", key: "movementModifier").nonEmpty)
    }

    private static func name(
        in container: KeyedDecodingContainer<WireKey>, _ label: String, key: String? = nil
    ) -> String {
        container.lenient(NamedWireObject.self, key ?? label)?.name ?? ""
    }
}

/// Any nested wire object whose only interesting member is its name.
private struct NamedWireObject: Decodable {
    let name: String

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        name = container.lenient("name", or: "")
    }
}

extension String {
    /// The string, or `nil` when it holds nothing but whitespace — for the many wire members where
    /// the server writes `""` and the domain means "absent".
    var nonEmpty: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}

/// Movement names by id, so a set row is one lookup rather than a scan.
///
/// A missing id is a real case and not a programming error: a session can outlive the movement it
/// referenced, and the catalogue read can fail while the session read succeeds. Both answer with
/// the same honest placeholder rather than hiding the set.
struct MovementCatalogue: Equatable, Sendable {
    private let namesById: [UUID: MovementData]

    init(_ movements: [MovementData] = []) {
        namesById = Dictionary(movements.map { ($0.movementDataId, $0) }, uniquingKeysWith: { first, _ in first })
    }

    var isEmpty: Bool { namesById.isEmpty }

    func movement(_ id: UUID) -> MovementData? { namesById[id] }

    /// What to print for a set's movement. `nil` is a set shape with no movement identity at all —
    /// a trail run, a freeform log — which is a different thing from a movement this catalogue
    /// could not resolve.
    func name(for id: UUID?) -> String {
        guard let id else { return "Unspecified" }
        return namesById[id]?.displayName ?? "Unknown movement"
    }
}
