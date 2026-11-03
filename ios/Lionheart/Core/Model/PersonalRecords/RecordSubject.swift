import Foundation

/// What a record is held against: one movement, or trail running, which names no movement.
///
/// Two cases rather than an optional id. The wire flattens them to an optional `movementDataId`,
/// absent meaning trail running, and an optional carried inward would make every screen re-decide
/// what a missing id means — "trail running" in one place, "unknown movement" in the next.
enum RecordSubject: Hashable, Sendable {
    case movement(UUID)
    case trailRunning

    init(wire movementDataId: UUID?) {
        self = movementDataId.map(RecordSubject.movement) ?? .trailRunning
    }

    /// The id the history route takes, which is absent for trail running exactly as it arrived.
    var movementDataId: UUID? {
        switch self {
        case .movement(let id): id
        case .trailRunning: nil
        }
    }

    /// What the subject is called. A movement the catalogue no longer lists still has records, so
    /// this answers with the catalogue's placeholder rather than hiding them.
    func name(in catalogue: MovementCatalogue) -> String {
        switch self {
        case .movement(let id): catalogue.name(for: id)
        case .trailRunning: "Trail running"
        }
    }
}

extension SetCommand {
    /// The subject a set competes for records on, or `nil` for a freeform set, which has none.
    var recordSubject: RecordSubject? {
        switch self {
        case .lift(let set): .movement(set.movementDataId)
        case .distanceTime(let set): .movement(set.movementDataId)
        case .trailRun: .trailRunning
        case .freeform: nil
        }
    }
}
