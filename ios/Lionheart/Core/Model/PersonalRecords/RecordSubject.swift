import Foundation

/// What a record is held against: one movement, or an activity that names none — trail running,
/// riding, hiking.
///
/// Cases rather than an optional id. The wire flattens them to an optional `movementDataId` and a
/// `subject` kind, and an optional carried inward would make every screen re-decide what a missing
/// id means — "trail running" in one place, "unknown movement" in the next.
enum RecordSubject: Hashable, Sendable {
    case movement(UUID)
    case trailRunning
    case riding
    case hiking

    /// From the wire's pair. A movement id is the movement whatever the kind says; with none, the
    /// kind names the activity, and a kind this build does not know — or none, from a server that
    /// predates riding and hiking — is trail running, which is what an absent id always meant.
    init(wire movementDataId: UUID?, subject: String?) {
        if let movementDataId {
            self = .movement(movementDataId)
            return
        }
        switch subject {
        case "riding": self = .riding
        case "hiking": self = .hiking
        default: self = .trailRunning
        }
    }

    /// The id the history route takes, which is absent for a subject that names no movement.
    var movementDataId: UUID? {
        switch self {
        case .movement(let id): id
        case .trailRunning, .riding, .hiking: nil
        }
    }

    /// The kind the history route takes beside an absent id.
    var wireKind: String {
        switch self {
        case .movement: "movement"
        case .trailRunning: "trailRunning"
        case .riding: "riding"
        case .hiking: "hiking"
        }
    }

    /// What the subject is called. A movement the catalogue no longer lists still has records, so
    /// this answers with the catalogue's placeholder rather than hiding them.
    func name(in catalogue: MovementCatalogue) -> String {
        switch self {
        case .movement(let id): catalogue.name(for: id)
        case .trailRunning: "Trail running"
        case .riding: "Riding"
        case .hiking: "Hiking"
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
        case .endurance(let set): set.activity.recordSubject
        case .freeform: nil
        }
    }
}

extension EnduranceActivity {
    /// The subject a set of this activity competes on: a ride on riding, a hike on hiking. Indoors
    /// the distance is the machine's estimate, and a walk is movement rather than an effort to rank,
    /// so neither competes — the server's `RecordCandidates` rule.
    var recordSubject: RecordSubject? {
        switch self {
        case .ride: .riding
        case .hike: .hiking
        case .indoorRide, .walk, .indoorRun, .unrecognised: nil
        }
    }
}
