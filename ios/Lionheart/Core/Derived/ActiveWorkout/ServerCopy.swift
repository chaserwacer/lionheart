import Foundation

/// What the server already holds of a workout the device has not had a clear answer about.
///
/// It exists because a request can land and its answer be lost. A start or a completion sent over a
/// failing connection may have committed on the server, and sending it again blindly would make a
/// second session: two copies of one workout, each with the same sets. So before a deferred start
/// is opened, and before a finished workout is delivered a second time, the device reads what the
/// server has and recognises its own work there.
///
/// Recognition is by **start instant**. The device chooses a workout's start, sends exactly that
/// instant, and the server stores it as sent, which makes the start the one key both sides already
/// share without the server having to accept a client-chosen id. A live start is to the second and
/// nothing else lands on it. A past workout's start is a time the athlete picked, to the minute,
/// and two of them can share one; so a completed session must also hold as many sets as the
/// workout to be it. Each set the device sends becomes one set on the server, and a completed
/// session's sets do not change afterwards.
enum ServerCopy: Equatable, Sendable {
    /// Nothing on the server is this workout.
    case absent
    /// The planned session the workout was started from, still planned. Starting it is how this
    /// workout gets its session.
    case planWaiting(UUID)
    /// An active session this device opened, which its sets can still be pushed to.
    case active(UUID)
    /// A completed session that is this workout, already delivered.
    case completed(UUID)

    /// How far apart two instants may be and still be the same start. The server keeps the
    /// instant as sent; the tolerance only absorbs a precision change on the way back.
    static let tolerance: TimeInterval = 1

    /// This workout's copy among `sessions`, read from a window around its start.
    static func among(_ sessions: [TrainingSession], of workout: ActiveWorkout) -> ServerCopy {
        sessions.lazy.map { carrying($0, workout) }.first { $0 != .absent } ?? .absent
    }

    /// What the plan a workout was started from says about it, or `nil` when the plan is no longer
    /// this workout's: started, skipped or completed somewhere else, or removed.
    ///
    /// - Parameter plan: the planned session as the server now holds it, or `nil` when it is gone.
    static func ofPlan(_ plan: TrainingSession?, of workout: ActiveWorkout) -> ServerCopy? {
        guard let plan else { return nil }
        if case .planned(let planned) = plan { return .planWaiting(planned.envelope.trainingSessionId) }
        let copy = carrying(plan, workout)
        switch copy {
        case .absent: return nil
        case .planWaiting, .active, .completed: return copy
        }
    }

    /// Whether `session` is this workout, begun or finished.
    ///
    /// A session in any other state, or one that began at another instant, is not: a planned or
    /// skipped session has no start to compare, and an active one started elsewhere is somebody
    /// else's workout even when it is the same athlete's. An active one's sets are not compared:
    /// they are what the next push replaces.
    private static func carrying(_ session: TrainingSession, _ workout: ActiveWorkout) -> ServerCopy {
        switch session {
        case .active(let active):
            return isSameStart(active.startedAt, workout.startedAt)
                ? .active(active.envelope.trainingSessionId) : .absent
        case .completed(let completed):
            guard let start = completed.period.startTime, isSameStart(start, workout.startedAt),
                  completed.sets.count == workout.sets.count
            else { return .absent }
            return .completed(completed.envelope.trainingSessionId)
        case .planned, .skipped, .unrecognised:
            return .absent
        }
    }

    private static func isSameStart(_ one: Date, _ other: Date) -> Bool {
        abs(one.timeIntervalSince(other)) <= tolerance
    }
}
