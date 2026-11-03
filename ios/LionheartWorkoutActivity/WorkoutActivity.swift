import ActivityKit
import SwiftUI
import WidgetKit

/// The workout's Live Activity: one Lock Screen design and three Dynamic Island ones.
///
/// They are four designs rather than one scaled four ways, because each answers a different
/// question in a different amount of room:
///
/// - **Lock Screen** — "how is the workout going?" The movement, the set count, the rest countdown
///   as the largest figure on screen, and anything the athlete has to act on. It is also what a
///   phone with no Dynamic Island shows.
/// - **Compact** — "how long until I lift again?" Two slivers either side of the camera, so it
///   carries one glyph and one figure: the rest countdown while resting, the session clock
///   otherwise. This is the presentation the athlete actually sees, because it is the one that
///   shows while they are using the phone.
/// - **Minimal** — a single circle, shown when something else is also using the Island. The glyph
///   alone, which is the only thing that fits; `WorkoutGlyph` records why a ring is not there.
/// - **Expanded** — what a long press reveals: the movement and set count on one side, the session
///   clock on the other, and the rest across the bottom with its bar.
///
/// The Dynamic Island exists only on iPhone 14 Pro and later. On every other phone the Lock Screen
/// presentation is the whole feature and the three Island closures are never called — which is why
/// the Lock Screen one is a full design in its own file rather than a fallback.
struct WorkoutActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: WorkoutActivityAttributes.self) { context in
            WorkoutLockScreenView(
                state: context.state,
                startedAt: context.attributes.startedAt)
        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    WorkoutIslandLeading(state: context.state)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    WorkoutElapsedClock(startedAt: context.attributes.startedAt)
                        .font(.title3.weight(.semibold))
                        .foregroundStyle(.primary)
                }
                DynamicIslandExpandedRegion(.bottom) {
                    WorkoutIslandBottom(state: context.state)
                }
            } compactLeading: {
                WorkoutGlyph(isResting: context.state.isResting)
            } compactTrailing: {
                // The rest comes first: a countdown is the only thing on this activity the athlete
                // is waiting on, and the session clock is always a long press away.
                if let rest = context.state.rest {
                    WorkoutRestClock(rest: rest)
                        .foregroundStyle(.tint)
                } else {
                    WorkoutElapsedClock(startedAt: context.attributes.startedAt)
                        .foregroundStyle(.tint)
                }
            } minimal: {
                WorkoutGlyph(isResting: context.state.isResting)
            }
            .keylineTint(.accentColor)
        }
    }
}
