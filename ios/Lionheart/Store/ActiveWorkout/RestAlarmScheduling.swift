import Foundation

/// Holding the alert that fires when the rest runs out.
///
/// A seam at a **system** boundary, earning a stand-in the same way `WorkoutSessionWriting` does at
/// the network one and `WorkoutActivityPresenting` does at the process one: what is being stood in
/// for is the notification centre, which a unit test cannot run, and the claims worth making are
/// about what is left scheduled after a sequence of events.
///
/// `schedule` is one method taking the whole answer, for the same reason `present` is: every way a
/// rest can start, change, expire or be abandoned arrives as the same question, so there is no
/// schedule-and-cancel pair to get out of order and no event that leaves an alarm behind.
@MainActor
protocol RestAlarmScheduling {
    /// Makes the pending alert match `alarm`, scheduling, replacing, or withdrawing it.
    ///
    /// - Parameter alarm: what should be waiting to fire, or `nil` for nothing.
    func schedule(_ alarm: PendingRestAlarm?) async

    /// Takes down any alert that has **already fired** and is sitting in Notification Centre.
    ///
    /// Separate from `schedule` because it is not about the rest: it is the sign-out path, where a
    /// delivered "Rest over — Barbell Back Squat" would still name the last athlete's session on a
    /// phone that has been signed out. Ordinary rests deliberately leave their delivered alert
    /// alone — the athlete saw it, and withdrawing it the moment they open the app would take it
    /// out from under them.
    func forgetDelivered() async
}

/// A phone that never buzzes, for a store running without one.
///
/// A value rather than an optional scheduler, matching `NoWorkoutActivity`: "nothing alerts this
/// workout" is a real arrangement and not a missing collaborator, and the store should have one
/// `schedule` call site rather than an optional to remember to chain through.
struct NoRestAlarm: RestAlarmScheduling {
    func schedule(_ alarm: PendingRestAlarm?) async {}
    func forgetDelivered() async {}
}
