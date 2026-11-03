import Foundation

/// The alert that should be waiting to fire when the rest runs out.
///
/// A rest clock the athlete cannot hear is half a timer, and the phone being in a pocket between
/// sets is the normal case rather than the edge one. The store's own clock is a `Task`, which does
/// not run while the app is suspended, so the thing that actually buzzes has to be held by the
/// system: a local notification, scheduled when the rest arms and withdrawn when it stops
/// mattering.
///
/// It is derived from the **rest**, not from the Live Activity's projection, because the two cover
/// different ground. Only a live session is worth a Lock Screen entry, but a workout being written
/// down afterwards is logged through the same surface and can arm a rest just the same — and an
/// athlete who armed one wants to hear it whichever kind of workout they are in.
///
/// `of` answering `nil` is the instruction to withdraw whatever is scheduled. That totality is the
/// same decision `WorkoutActivityProjection` makes, for the same reason: the caller never has to
/// tell "no workout" from "no rest" from "a rest that has already run out", so there is no path on
/// which an alarm is left behind to buzz about a rest the athlete skipped.
struct PendingRestAlarm: Equatable, Sendable {
    /// When the rest runs out. The instant rather than a duration, so that comparing the alarm
    /// that should exist against the one already scheduled does not drift with every call.
    let endsAt: Date

    /// The movement whose rest this is, where one is known.
    ///
    /// It is here because the notification has to say it. A phone carrying three apps' timers
    /// answers "Rest over" with "which rest?", and the athlete should not have to unlock to find
    /// out.
    let movementTitle: String?

    /// The alarm this workout warrants at `now`, or `nil` for none.
    ///
    /// - Parameter now: a rest whose time is already up answers `nil` rather than an alarm in the
    ///   past. The store retires such a clock a moment later, and a notification cannot be
    ///   scheduled for an instant that has gone.
    static func of(_ workout: ActiveWorkout?, movementTitle: String?, at now: Date)
        -> PendingRestAlarm? {
        guard let workout, case .running(let since, let duration) = workout.rest,
              workout.rest.isResting(at: now)
        else { return nil }

        return PendingRestAlarm(
            endsAt: since.addingTimeInterval(duration),
            movementTitle: movementTitle.flatMap { $0.isEmpty ? nil : $0 })
    }

    /// How long from `now` until it should fire. Never zero or negative, which is what a
    /// notification trigger refuses.
    func secondsRemaining(at now: Date) -> TimeInterval {
        max(0, endsAt.timeIntervalSince(now))
    }

    /// What the notification says.
    ///
    /// Short because it is read on a Lock Screen at arm's length, and it names the movement so it
    /// is answerable without unlocking.
    var title: String { "Rest over" }

    var body: String {
        guard let movementTitle else { return "Time for your next set." }
        return "Time for your next set of \(movementTitle)."
    }
}
