import Foundation

/// The records standing against one movement when the workout began.
///
/// Two figures rather than the server's two whole `PersonalRecord` rows, because during a workout
/// the only question is *did this set beat it*: the date it was set, the row's own id and the chain
/// back to the record it replaced all belong to the History tab. Either may be absent — a movement
/// with no record yet is the common case for a new athlete.
///
/// `heaviestWeight` and `bestVolume` are the server's two `PersonalRecordType` cases: Strength
/// compares weight alone, Volume compares weight × reps.
struct MovementRecords: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let heaviestWeight: Double?
    let bestVolume: Double?

    var id: UUID { movementDataId }

    /// Whether there is anything here to beat. A summary row with neither figure is a movement the
    /// server has no record for, which reads as "no record yet" rather than as a record of zero.
    var isEmpty: Bool { heaviestWeight == nil && bestVolume == nil }
}

extension MovementRecords: Decodable {
    /// `MovementDataPRSummary`, which carries each record as a whole `PersonalRecordDTO`.
    ///
    /// The keys follow the same `…ID` spelling as `MovementDataDTO` beside it — see
    /// `MovementData.init(from:)` for why two spellings are live at once.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let movementDataId = container.lenient(UUID.self, "movementDataID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A record summary needs a movementDataID."))
        }

        self.movementDataId = movementDataId
        heaviestWeight = container.lenient(Record.self, "strengthPR")?.weight
        bestVolume = container.lenient(Record.self, "volumePR")?.volume
    }
}

/// The two figures a `PersonalRecordDTO` contributes. `volume` is the server's own `Weight * Reps`,
/// read rather than recomputed so the device cannot disagree with it about a stored record.
private struct Record: Decodable {
    let weight: Double?
    let volume: Double?

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        weight = container.lenient(Double.self, "weight")
        volume = container.lenient(Double.self, "volume")
    }
}
