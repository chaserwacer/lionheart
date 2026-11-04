import Foundation

/// What the server made of one Apple Health workout the phone offered (`POST /api/health/workouts`).
///
/// Every case is an answer: a workout already imported, or one deliberately left out, is the import
/// working as intended, and the sync moves on to the next either way.
enum HealthImportOutcome: Equatable, Sendable {
    case imported(trainingSessionId: UUID)
    case alreadyImported(trainingSessionId: UUID)
    case notImported(HealthWorkoutExclusion)
    case unrecognised(String)
}

/// What the server said to one workout offered: an answer, or its refusal of that one workout in
/// its own words — a workout it could not read, or one it would not write yet.
enum HealthOfferReply: Equatable, Sendable {
    case answered(HealthImportOutcome)
    case refused(String)
}

/// Why the server left an Apple Health workout out of the log.
enum HealthWorkoutExclusion: Hashable, Sendable {
    /// Written by an app Lionheart reads by its own route — Oura or Strong.
    case readDirectly
    /// Not a run, ride, hike or walk: elliptical, stairs and the rest.
    case otherActivity
    case noDistance
    /// The same session as one already in the log — another app's copy of it, or a logged one.
    case sameAsLoggedRun
    /// Inside a logged lifting session.
    case insideLoggedLifting
    /// Imported once, then deleted from the log.
    case deleted
    case unrecognised(String)

    /// The reason as a phrase after a count: "2 recorded by Oura or Strong".
    var phrase: String {
        switch self {
        case .readDirectly: "recorded by Oura or Strong, which Lionheart reads directly"
        case .otherActivity: "of a kind Lionheart does not take"
        case .noDistance: "with no distance"
        case .sameAsLoggedRun: "already in the log"
        case .insideLoggedLifting: "inside a logged lifting session"
        case .deleted: "deleted from the log"
        case .unrecognised(let wire): wire
        }
    }

    init(wire: String) {
        switch wire {
        case "readDirectly": self = .readDirectly
        case "otherActivity": self = .otherActivity
        case "noDistance": self = .noDistance
        case "sameAsLoggedRun": self = .sameAsLoggedRun
        case "insideLoggedLifting": self = .insideLoggedLifting
        case "deleted": self = .deleted
        default: self = .unrecognised(wire)
        }
    }
}

extension HealthImportOutcome: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "imported":
            guard let session = container.lenient(TrainingSession.self, "session") else {
                self = .unrecognised(type)
                return
            }
            self = .imported(trainingSessionId: session.id)
        case "alreadyImported":
            guard let id = container.lenient(UUID.self, "trainingSessionId") else {
                self = .unrecognised(type)
                return
            }
            self = .alreadyImported(trainingSessionId: id)
        case "notImported":
            self = .notImported(HealthWorkoutExclusion(wire: container.lenient("reason", or: "")))
        default:
            self = .unrecognised(type)
        }
    }
}
