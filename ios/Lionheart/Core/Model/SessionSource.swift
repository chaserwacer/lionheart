import Foundation

/// Where a session came from: the athlete's own log, or a workout another app recorded and Apple
/// Health holds.
///
/// Orthogonal to `Provenance`, which says who authored it. An imported run has a route and the
/// recording app's heart rate behind it, which is what its run page reads.
enum SessionSource: Equatable, Sendable {
    /// Entered in Lionheart, or imported from the athlete's own logs.
    case athleteLog
    /// Imported from Apple Health. `recordedBy` is the app that wrote it there, as Health names it.
    case appleHealth(workoutId: UUID, recordedBy: String)
    case unrecognised(String)

    /// Whether a run page stands behind the session.
    var isRecorded: Bool {
        switch self {
        case .appleHealth: true
        case .athleteLog, .unrecognised: false
        }
    }
}

extension SessionSource: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "athleteLog":
            self = .athleteLog
        case "appleHealth":
            guard let workoutId = container.lenient(UUID.self, "workoutId") else {
                self = .unrecognised(type)
                return
            }
            let app = container.lenient(RecordingApp.self, "recordedBy")
            self = .appleHealth(workoutId: workoutId, recordedBy: app?.name ?? "")
        default:
            self = .unrecognised(type)
        }
    }

    /// The app half of the wire's `recordedBy`; the bundle identifier is the server's business.
    private struct RecordingApp: Decodable {
        let name: String
    }
}
