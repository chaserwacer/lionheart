import SwiftUI

/// The two clocks the workout's Live Activity shows, and the glyph that says which one matters.
///
/// Both clocks are rendered **by the system** from a date range. Nothing in this extension counts,
/// and the app sends no update as a second passes: WidgetKit redraws a `Text(timerInterval:)` on
/// its own, which is what makes a rest countdown stay right while the app is suspended in a
/// pocket. An activity that was pushed a number once a second would be wrong the moment the app
/// stopped running, and would cost a cross-process write per tick to be wrong.
///
/// They are shared by the Lock Screen and all three Island presentations so that the figures agree
/// across surfaces the athlete may be looking at within a second of each other.

/// How long after its start an activity's elapsed clock can still be counting.
///
/// `Text(timerInterval:)` needs a closed range and counts up from its lower bound, so the upper one
/// is a bound the clock will never reach rather than a meaningful instant. Twelve hours is past the
/// longest a Live Activity is allowed to live, so the system has ended the activity before this
/// matters — `Date.distantFuture` would do the same job and is a less honest number to read.
private let longestPossibleActivity = TimeInterval(12 * 60 * 60)

/// Time since the session started, counting up, as the system draws it.
///
/// Hours are left on: a session past an hour should read `1:22:14` here and in the in-app bar,
/// which formats it the same way.
struct WorkoutElapsedClock: View {
    let startedAt: Date

    var body: some View {
        Text(
            timerInterval: startedAt...startedAt.addingTimeInterval(longestPossibleActivity),
            countsDown: false)
            .monospacedDigit()
    }
}

/// What is left of the rest, counting down, as the system draws it.
///
/// `showsHours: false` because no rest is an hour long, and with hours on a 101-second countdown
/// reads `0:01:41`. It is a display choice and not a layout one: WidgetKit reserves a fixed width
/// for live timer text in the Island's compact slot whatever the text says, which was measured —
/// the slot reports the same 102pt with hours, without them, and under `fixedSize()`.
struct WorkoutRestClock: View {
    let rest: WorkoutActivityAttributes.Rest

    var body: some View {
        Text(timerInterval: rest.interval, countsDown: true, showsHours: false)
            .monospacedDigit()
    }
}

/// Which of the two things the athlete is doing.
///
/// An hourglass while resting and the lifting figure while working, matching the in-app bar, so the
/// Lock Screen and the bar are recognisably the same thing in two frames.
///
/// It is also the whole of the **minimal** Island presentation, which is one circle about a glyph
/// wide. A draining ring was the obvious thing to want there and was tried:
/// `ProgressView(timerInterval:)` under `.circular` renders as the indeterminate spinner, which
/// says "loading" rather than "resting, this far through" — the opposite of the one thing that slot
/// has room to say. The glyph still distinguishes resting from working, which is the only
/// distinction that fits in a circle that size.
struct WorkoutGlyph: View {
    let isResting: Bool

    var body: some View {
        Image(systemName: isResting ? "hourglass" : "figure.strengthtraining.traditional")
            .foregroundStyle(.tint)
            .accessibilityHidden(true)
    }
}

extension WorkoutActivityAttributes.ContentState {
    /// What this workout is called on a surface with room for a name.
    ///
    /// The movement under the athlete's thumb is what they are looking for; before one is chosen
    /// the activity says what it is rather than showing an empty line.
    var headline: String { movementTitle ?? "Workout in progress" }

    var isResting: Bool { rest != nil }

    /// The set count in words. Said the same way on every surface that has room for it.
    var setCountPhrase: String { setCount == 1 ? "1 set" : "\(setCount) sets" }
}
