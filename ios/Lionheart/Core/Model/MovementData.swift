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
    /// The athlete's own notes on the movement, and how its weight is read.
    let setup: MovementSetup
    /// The base this movement is a variation of; what a classification is stated against. `nil`
    /// only from a payload that drifted.
    let movementBaseId: UUID?
    /// What the base trains, shared by every variation of it.
    let classification: ClassificationStanding

    var id: UUID { movementDataId }

    /// The same movement with the setup the server answered after a change to it.
    func with(setup: MovementSetup) -> MovementData {
        MovementData(
            movementDataId: movementDataId, baseName: baseName, equipmentName: equipmentName,
            modifierName: modifierName, setup: setup, movementBaseId: movementBaseId,
            classification: classification)
    }

    /// The same movement with the base's classification as the server answered it after a change.
    func with(classification: ClassificationStanding) -> MovementData {
        MovementData(
            movementDataId: movementDataId, baseName: baseName, equipmentName: equipmentName,
            modifierName: modifierName, setup: setup, movementBaseId: movementBaseId,
            classification: classification)
    }

    /// Whether the plate calculator and the warm-up ramp's bar step apply: a straight barbell,
    /// loaded with plates on a 45 lb bar. Smith machines and specialty bars weigh something else,
    /// so they are left out rather than given a wrong breakdown.
    var isBarbell: Bool {
        equipmentName.trimmingCharacters(in: .whitespaces).caseInsensitiveCompare("Barbell") == .orderedSame
    }

    /// The movement as the athlete names it: the variation qualifying the exercise, "Seated Leg
    /// Curl". The modifier is left out when the base already says it ("Incline Bench Press" with
    /// "incline").
    ///
    /// The server stores a modifier lower-cased however it was created, so its words are
    /// capitalised here, short joining words apart ("Behind-the-Back"). The base keeps the
    /// capitalisation the server holds, because that is the athlete's own.
    var title: String {
        let base = baseName.trimmingCharacters(in: .whitespaces)
        guard let variation = modifierName?.trimmingCharacters(in: .whitespaces), !variation.isEmpty,
              !Self.says(base, variation)
        else { return base }
        return Self.says(variation, base) ? Self.capitalised(variation) : "\(Self.capitalised(variation)) \(base)"
    }

    /// The equipment, shown beside the title rather than inside it.
    var qualifier: String { equipmentName }

    /// The whole movement as one phrase, for somewhere with room for only one — a section header,
    /// a record row, a set list — where `title` and `qualifier` cannot sit side by side: "Seated
    /// Leg Curl (Machine)", the library's convention and Strong's.
    ///
    /// Where one part already says another, as whole words, the shorter is left out, so a movement
    /// whose parts overlap cannot read twice: "T-Bar Row" on a T-Bar, and "Standing Leg Curl"
    /// where the machine was stored as the equipment. The server composes the same phrase for the
    /// chat (`MovementNames.Of`); the two are held together by their tests.
    var displayName: String {
        let title = baseName.trimmingCharacters(in: .whitespaces).isEmpty ? "Unnamed movement" : self.title
        let kit = equipmentName.trimmingCharacters(in: .whitespaces)
        if kit.isEmpty || Self.says(title, kit) { return title }
        return Self.says(kit, title) ? kit : "\(title) (\(kit))"
    }

    private static let joiningWords: Set<String> = ["a", "and", "of", "on", "the", "to"]

    /// Every word capitalised but a joining word inside the phrase.
    static func capitalised(_ modifier: String) -> String {
        modifier.replacing(/\b\p{L}[\p{L}']*/) { match in
            let word = String(match.output)
            let isInside = match.range.lowerBound != modifier.startIndex
            return isInside && joiningWords.contains(word.lowercased())
                ? word
                : word.prefix(1).uppercased() + word.dropFirst()
        }
    }

    /// Whether `phrase` holds `part` as whole words, ignoring case.
    private static func says(_ phrase: String, _ part: String) -> Bool {
        let words = phrase.split(separator: " ")
        let wanted = part.split(separator: " ")
        guard !wanted.isEmpty, words.count >= wanted.count else { return false }
        return (0...(words.count - wanted.count)).contains { start in
            zip(words[start..<(start + wanted.count)], wanted).allSatisfy {
                $0.caseInsensitiveCompare($1) == .orderedSame
            }
        }
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
        let movementBase = container.lenient(BaseEntity.self, "movementBase")
        baseName = movementBase?.name ?? ""
        movementBaseId = movementBase?.movementBaseId
        classification = movementBase?.classification ?? .unclassified
        equipmentName = container.lenient(NamedEntity.self, "equipment")?.name ?? ""
        modifierName = container.lenient(NamedEntity.self, "movementModifier")?.name
        setup = container.lenient(MovementSetup.self, "setup") ?? .none
    }
}

/// What the athlete keeps about a movement: setup cues — seat positions, grip, pin — shown while
/// logging it, and whether the weight logged on it is plate added to their own bodyweight.
///
/// The second is what makes a weighted dip readable. "+45" is the plate on the belt; the load the
/// athlete moved is their bodyweight plus that, and the plate alone cannot be compared across a
/// year in which the bodyweight moved.
struct MovementSetup: Equatable, Sendable {
    let notes: String
    let addsToBodyweight: Bool

    static let none = MovementSetup(notes: "", addsToBodyweight: false)

    var hasNotes: Bool { !notes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    /// The longest notes the server keeps.
    static let notesLimit = 2000
}

extension MovementSetup: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        notes = container.lenient("notes", or: "")
        addsToBodyweight = container.lenient("addsToBodyweight", or: false)
    }
}

/// The movement base: its name, its id, and what it trains. Its description belongs to the screens
/// that edit the catalogue, which live on the web app.
private struct BaseEntity: Decodable {
    let name: String
    let movementBaseId: UUID?
    let classification: ClassificationStanding

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        name = container.lenient("name", or: "")
        movementBaseId = container.lenient(UUID.self, "movementBaseID")
        classification = container.lenient(ClassificationStanding.self, "classification") ?? .unclassified
    }
}

/// Anything on the wire whose only interesting member is its name: equipment and a modifier carry
/// more, none of which the client shows.
private struct NamedEntity: Decodable {
    let name: String

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        name = container.lenient("name", or: "")
    }
}
