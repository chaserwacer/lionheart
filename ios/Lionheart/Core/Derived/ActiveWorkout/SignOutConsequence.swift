import Foundation

/// What signing out costs the workout in flight, said before the athlete agrees to it.
///
/// Signing out deletes the workout document (`ActiveWorkoutStore.reset`), deliberately: a document
/// the next sign-in could adopt would push one athlete's sets at another's session. So the honest
/// answer depends on what the server already holds, and these are the different answers — one per
/// thing the athlete stands to lose: none, none that cannot be resumed, the sets not yet pushed, or
/// everything.
enum SignOutConsequence: Equatable, Sendable {
    /// No workout on this device.
    case nothingInFlight
    /// A live session whose every set the server has. It stays active there, so Train offers to
    /// resume it.
    case resumableFromTrain
    /// A live session with sets the server does not have yet — pending, failed or refused.
    case unsyncedSetsLost
    /// A workout started with no connection whose session the server has never opened.
    case unopenedWorkoutLost
    /// A workout being written down afterwards. Nothing is sent until it is finished.
    case pastWorkoutLost

    static func of(_ workout: ActiveWorkout?) -> SignOutConsequence {
        guard let workout else { return .nothingInFlight }
        switch workout.destination {
        case .afterTheFact:
            return .pastWorkoutLost
        case .deferredStart:
            return .unopenedWorkoutLost
        case .liveSession:
            switch workout.sync {
            case .synced: return .resumableFromTrain
            case .pending, .failed, .refused: return .unsyncedSetsLost
            }
        }
    }

    /// The confirmation's message.
    var message: String {
        switch self {
        case .nothingInFlight:
            "Anything logged is on the server."
        case .resumableFromTrain:
            "The workout in progress leaves this device. Its sets are on the server, and Train can resume it."
        case .unsyncedSetsLost:
            "The workout in progress leaves this device, and sets not yet on the server are lost."
        case .unopenedWorkoutLost:
            "The workout in progress has not reached the server and is lost."
        case .pastWorkoutLost:
            "The past workout being logged is lost. Nothing is sent until it is finished."
        }
    }

    /// The confirmation's message, counting the finished workouts still waiting to be delivered,
    /// which leave the device with everything else.
    func message(undelivered: Int) -> String {
        guard undelivered > 0 else { return message }
        let waiting = undelivered == 1
            ? "A finished workout not yet on the server is lost."
            : "\(undelivered) finished workouts not yet on the server are lost."
        switch self {
        case .nothingInFlight:
            return waiting
        case .resumableFromTrain, .unsyncedSetsLost, .unopenedWorkoutLost, .pastWorkoutLost:
            return "\(message) \(waiting)"
        }
    }
}
