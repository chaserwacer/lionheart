import Foundation

/// Where a session came from: the athlete's own log, a workout another app recorded and Apple
/// Health holds, or an activity from the athlete's Strava archive.
///
/// Orthogonal to `Provenance`, which says who authored it. An imported run has a route and the
/// recording app's heart rate behind it, which is what its run page reads.
enum SessionSource: Equatable, Sendable {
    /// Entered in Lionheart, or imported from the athlete's own logs.
    case athleteLog
    /// Imported from Apple Health. `recordedBy` is the app that wrote it there, as Health names it.
    case appleHealth(workoutId: UUID, recordedBy: String)
    /// Imported from the archive Strava exports; `activityId` is Strava's own.
    case stravaArchive(activityId: Int64)
    case unrecognised(String)

    /// Whether the session is a device's recording, so a run page may stand behind it. An archived
    /// gym session is one with no page: Strava kept no route for it.
    var isRecorded: Bool {
        switch self {
        case .appleHealth, .stravaArchive: true
        case .athleteLog, .unrecognised: false
        }
    }

    /// Who recorded it, as the screens name them: the app Health named, or Strava.
    var recordedBy: String? {
        switch self {
        case .appleHealth(_, let recordedBy): recordedBy.isEmpty ? nil : recordedBy
        case .stravaArchive: "Strava"
        case .athleteLog, .unrecognised: nil
        }
    }

    /// Where it was imported from, as a sentence names it.
    var importedFrom: String? {
        switch self {
        case .appleHealth: "Apple Health"
        case .stravaArchive: "your Strava archive"
        case .athleteLog, .unrecognised: nil
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
        case "stravaArchive":
            guard let activityId = container.lenient(Int64.self, "activityId") else {
                self = .unrecognised(type)
                return
            }
            self = .stravaArchive(activityId: activityId)
        default:
            self = .unrecognised(type)
        }
    }

    /// The app half of the wire's `recordedBy`; the bundle identifier is the server's business.
    private struct RecordingApp: Decodable {
        let name: String
    }
}
