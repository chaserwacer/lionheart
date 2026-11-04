import Foundation

/// The Live Activity a workout warrants, derived from the workout and nothing else.
///
/// It is one function rather than a handful read by the extension because the two representations
/// must not be able to drift: everything the Lock Screen says is decided here, from the same value
/// the in-app bar is drawn from, and the extension only lays the answers out. Being pure is what
/// lets the awkward cases — a rest that has already expired, a workout with no movement chosen, a
/// retro log that should have no activity at all — be stated as tests rather than discovered on a
/// Lock Screen.
///
/// `of` returning `nil` is the instruction to **end** whatever is showing. That is deliberate
/// totality: the caller never has to work out which of "no workout", "the wrong kind of workout"
/// and "a workout that no longer warrants an activity" it is looking at, so there is no path on
/// which an activity is left behind.
struct WorkoutActivityProjection: Equatable, Sendable {
    /// Fixed for the activity's life. Its `sessionId` is what matches an activity found at startup
    /// against the workout restored from disk.
    let attributes: WorkoutActivityAttributes
    /// What it says right now.
    let state: WorkoutActivityAttributes.ContentState

    /// The activity this workout warrants, or `nil` when it warrants none.
    ///
    /// - Parameters:
    ///   - isPersisted: whether the workout is also on disk. A separate fact from
    ///     `ActiveWorkout.sync`, and the more urgent one, so it leads the concern.
    ///   - now: used only to decide whether a rest still has time to run. A rest the store has not
    ///     yet retired reads as over rather than as a countdown into the negative.
    static func of(
        _ workout: ActiveWorkout?,
        movementTitle: String?,
        isPersisted: Bool,
        at now: Date
    ) -> WorkoutActivityProjection? {
        // Only a live session. A workout being written down afterwards has no clock the athlete
        // wants ticking at them and no rest to count, so a Lock Screen entry for it would be noise
        // about something they are holding the phone to type.
        guard let workout, let sessionId = identity(of: workout.destination) else { return nil }

        return WorkoutActivityProjection(
            attributes: WorkoutActivityAttributes(
                sessionId: sessionId,
                startedAt: workout.startedAt),
            state: WorkoutActivityAttributes.ContentState(
                movementTitle: movementTitle.flatMap { $0.isEmpty ? nil : $0 },
                setCount: workout.workSetCount,
                rest: rest(of: workout.rest, at: now),
                concern: concern(of: workout.sync, isPersisted: isPersisted)))
    }

    /// What the activity is keyed by: the session's id, or, for a deferred start, the device's own
    /// id for the workout until the server opens one. The key changes once when that happens, and
    /// the presenter ends the old activity and requests the new one; the elapsed clock carries on
    /// unbroken because it counts from the workout's start, which does not move.
    private static func identity(of destination: WorkoutDestination) -> UUID? {
        switch destination {
        case .liveSession(let sessionId): sessionId
        case .deferredStart(let localId): localId
        case .afterTheFact: nil
        }
    }

    /// The rest as two instants, or `nil` where there is nothing to count down.
    ///
    /// A `.running` clock whose time is already up answers `nil` rather than an empty range: the
    /// store retires it a moment later, and until it does the Lock Screen should read as working
    /// rather than as resting for zero seconds.
    private static func rest(of clock: RestClock, at now: Date) -> WorkoutActivityAttributes.Rest? {
        switch clock {
        case .idle:
            return nil
        case .running(let since, let duration):
            guard clock.isResting(at: now) else { return nil }
            return WorkoutActivityAttributes.Rest(
                startedAt: since,
                endsAt: since.addingTimeInterval(duration))
        }
    }

    /// The one thing worth saying about where the workout stands, most pressing first.
    ///
    /// The order is the in-app bar's, and the sentences are too, so a glance at the Lock Screen and
    /// a glance at the bar never tell the athlete two different stories. Unsaved leads because it
    /// is the only one they can act on; `.pending` says nothing at all, because almost every set is
    /// pending for a second or two and warning about it would teach them to ignore the line.
    private static func concern(of sync: WorkoutSyncState, isPersisted: Bool) -> String? {
        guard isPersisted else { return "Not saved to this device" }
        switch sync {
        case .refused(let reason): return reason
        case .failed: return "Waiting to sync"
        case .synced, .pending: return nil
        }
    }
}
