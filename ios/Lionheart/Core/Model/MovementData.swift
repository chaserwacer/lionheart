import Foundation

/// The identity of a thing you do: a movement base, the equipment it is done with, and an optional
/// modifier.
///
/// The athlete picks one of these to log a set against, so the only question this type has to
/// answer well is *what does it read as* — hence `title` and `qualifier` rather than a single
/// name. Splitting them lets the picker and the set row show "Incline bench press" large and
/// "Dumbbell" small, which is how a list of forty movements stays scannable.
///
/// It carries no modality. Whether a movement is lifted or run for distance is a property of the
/// *set*, not of the movement — the same treadmill is both — and `SetModality` is where that lives.
struct MovementData: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let baseName: String
    let equipmentName: String
    let modifierName: String?

    var id: UUID { movementDataId }

    /// The movement as the athlete names it: the modifier qualifies the base.
    ///
    /// Only the **modifier's** first letter is forced upper case. The server stores a modifier's
    /// name lower-cased however it was created, so "incline bench press" sitting under "Back squat"
    /// in a list reads as a bug — but everything else keeps the capitalisation the server holds,
    /// because that is the athlete's own and this client has no better information about it.
    var title: String {
        guard let modifierName, !modifierName.isEmpty else { return baseName }
        return "\(Self.capitalisingFirst(modifierName)) \(baseName)"
    }

    private static func capitalisingFirst(_ name: String) -> String {
        guard let first = name.first else { return name }
        return first.uppercased() + name.dropFirst()
    }

    /// The equipment, shown beside the title rather than inside it.
    var qualifier: String { equipmentName }

    /// The whole movement as one phrase, for somewhere with room for only one — a section header,
    /// a record row — where `title` and `qualifier` cannot sit side by side. The equipment sits
    /// where a lifter says it, between the modifier and the base.
    ///
    /// It is dropped when the name already contains the word, because a naive join reads
    /// "Barbell Barbell Row".
    var displayName: String {
        let base = baseName.isEmpty ? "Unnamed movement" : baseName
        var words: [String] = []
        if let modifierName, !modifierName.isEmpty {
            words.append(Self.capitalisingFirst(modifierName))
        }
        if !equipmentName.isEmpty, !base.localizedCaseInsensitiveContains(equipmentName) {
            words.append(equipmentName)
        }
        words.append(base)
        return words.joined(separator: " ")
    }

    /// Whether this movement answers `query`, matched against everything the athlete can see.
    ///
    /// Case- and diacritic-insensitive so "incline" finds "Incline", and matched on each word
    /// separately so "bench db" finds the dumbbell bench press — typing two half-words is faster
    /// than getting one whole phrase right.
    func matches(_ query: String) -> Bool {
        let words = query.split(whereSeparator: \.isWhitespace)
        guard !words.isEmpty else { return true }

        let haystack = "\(title) \(qualifier)"
        return words.allSatisfy { word in
            haystack.range(of: word, options: [.caseInsensitive, .diacriticInsensitive]) != nil
        }
    }
}

extension MovementData: Decodable {
    /// The one decode in this folder that reads `…ID` rather than `…Id`.
    ///
    /// `MovementDataDTO` predates the data remodel and spells its keys from C# properties named
    /// `MovementDataID`, which camelCase leaves as `movementDataID`. The remodelled set types next
    /// to it spell the same identifier `movementDataId`. Both are live on the server at once, so
    /// both are read here, and a reader comparing the two files is not seeing a typo.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let movementDataId = container.lenient(UUID.self, "movementDataID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A movement needs a movementDataID."))
        }

        self.movementDataId = movementDataId
        baseName = container.lenient(NamedEntity.self, "movementBase")?.name ?? ""
        equipmentName = container.lenient(NamedEntity.self, "equipment")?.name ?? ""
        modifierName = container.lenient(NamedEntity.self, "movementModifier")?.name
    }
}

/// Anything on the wire whose only interesting member is its name.
///
/// `Equipment`, `MovementBase` and `MovementModifier` all carry more than this, and none of it is
/// anything the client shows: a base's description and muscle groups belong to the screens that
/// edit the catalogue, which live on the web app.
private struct NamedEntity: Decodable {
    let name: String

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        name = container.lenient("name", or: "")
    }
}
