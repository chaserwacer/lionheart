import Foundation

/// A record on one movement, as the server holds it.
///
/// Volume is **received, not multiplied here**: the server derives it (`PersonalRecord.Volume`), and
/// a second derivation on the device is how two numbers for one record start to disagree.
///
/// `previousAchievedAt` is the fact that makes a record readable as progress rather than a number —
/// how long the last one stood. It is absent for a first record, which is a different thing from a
/// gap of zero.
struct PersonalRecord: Equatable, Sendable, Identifiable {
    let personalRecordId: UUID
    let movementDataId: UUID
    let movement: MovementData?
    let kind: Kind
    let weight: Double
    let unit: WeightUnit
    let reps: Int
    let volume: Double
    let achievedAt: Date?
    let previousAchievedAt: Date?
    let isActive: Bool

    var id: UUID { personalRecordId }

    /// Which comparison the record is on. Integer-valued on the wire; the order is the server's
    /// `PersonalRecordType`.
    enum Kind: Equatable, Sendable, Hashable {
        /// Heaviest weight moved, whatever the rep count.
        case strength
        /// Most weight times reps in one set.
        case volume
        case unrecognised(Int)

        init(wire: Int?) {
            switch wire {
            case 0: self = .strength
            case 1: self = .volume
            default: self = .unrecognised(wire ?? -1)
            }
        }

        var label: String {
            switch self {
            case .strength: "Strength"
            case .volume: "Volume"
            case .unrecognised: "Record"
            }
        }
    }
}

extension PersonalRecord: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let personalRecordId = container.lenient(UUID.self, "personalRecordID"),
              let movementDataId = container.lenient(UUID.self, "movementDataID")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A personal record needs its own id and the movement it is on."))
        }

        self.init(
            personalRecordId: personalRecordId,
            movementDataId: movementDataId,
            movement: container.lenient(MovementData.self, "movementData"),
            kind: Kind(wire: container.lenient(Int.self, "prType")),
            weight: container.lenient("weight", or: 0),
            unit: WeightUnit(wire: container.lenient(Int.self, "weightUnit")),
            reps: container.lenient("reps", or: 0),
            volume: container.lenient("volume", or: 0),
            achievedAt: container.instant("createdAt"),
            previousAchievedAt: container.instant("previousPRCreatedAt"),
            isActive: container.lenient("isActive", or: true))
    }
}

/// Both of a movement's current records, as `GET /api/personal-records/summaries` groups them.
///
/// Either can be absent: a movement only ever trained for reps has a volume record and no strength
/// one. Two optionals rather than a list, because the two are different questions and a screen
/// labels them differently.
struct MovementRecords: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let movement: MovementData?
    let strength: PersonalRecord?
    let volume: PersonalRecord?
    let lastRecordAt: Date?

    var id: UUID { movementDataId }

    var isEmpty: Bool { strength == nil && volume == nil }
}

extension MovementRecords: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let movementDataId = container.lenient(UUID.self, "movementDataID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A record summary without a movement id has nothing to label."))
        }

        self.init(
            movementDataId: movementDataId,
            movement: container.lenient(MovementData.self, "movementData"),
            strength: container.lenient(PersonalRecord.self, "strengthPR"),
            volume: container.lenient(PersonalRecord.self, "volumePR"),
            lastRecordAt: container.instant("lastPRDate"))
    }
}
