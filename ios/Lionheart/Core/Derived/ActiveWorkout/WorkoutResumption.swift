import Foundation

/// Taking over a session the server already holds as the workout the device owns.
///
/// It exists because the device is not the only way a session can become active: the web app can
/// start one, the app can be reinstalled mid-workout, or a workout can be discarded locally while
/// the server's session survives. In all three the Train tab sees an active session it has no
/// document for, and resuming is how the athlete carries on.
///
/// The refusal is the important part. `PUT /{id}/sets` replaces the **whole** list, so a set this
/// build cannot restate as a `SetCommand` would be deleted from the server by the next push — the
/// athlete would lose work by logging more. So a session carrying one is refused outright rather
/// than resumed lossily, and the UI says why.
enum WorkoutResumption {
    enum Refusal: Error, Equatable {
        /// A set shape this build cannot write back, named by its discriminator.
        case cannotRestate(type: String)
    }

    /// The session's sets as commands this build can push again.
    ///
    /// - Parameter loggedAt: stamped on every set, because the server stores no per-set instant.
    ///   The session's own start is the honest answer: it is the only time the device knows these
    ///   sets happened at or after.
    /// - Throws: `Refusal.cannotRestate` on the first set that cannot survive a round trip.
    static func sets(
        of session: TrainingSession.Active, loggedAt: Date
    ) throws(Refusal) -> [LoggedSet] {
        var restated: [LoggedSet] = []
        for entry in session.sets {
            restated.append(LoggedSet(loggedAt: loggedAt, command: try command(of: entry)))
        }
        return restated
    }

    private static func command(of entry: SetEntry) throws(Refusal) -> SetCommand {
        guard let command = entry.command else {
            // Only an unrecognised shape has no command.
            if case .unrecognised(let type, _) = entry { throw .cannotRestate(type: type) }
            throw .cannotRestate(type: "")
        }
        return command
    }
}
