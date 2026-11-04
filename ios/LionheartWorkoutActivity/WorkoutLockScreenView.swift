import ActivityKit
import SwiftUI
import WidgetKit

/// The workout on the Lock Screen, and in the banner the Island expands into on a phone without one.
///
/// This is the surface the whole feature is for: a rest countdown the athlete can read without
/// unlocking. It is laid out so that the countdown is the largest thing on it while a rest is
/// running, and the elapsed clock is the largest thing when none is — the question changes, so the
/// answer in the big type changes with it, rather than one fixed layout showing both at once.
///
/// It reads as the in-app bar in a different frame: the same glyph, the same movement name on top,
/// the same sentence about an unsynced workout, the app's Brick accent, the app's plain figures
/// rather than a rounded face, and otherwise system semantic colours so light and dark are right
/// without a colour literal.
struct WorkoutLockScreenView: View {
    let state: WorkoutActivityAttributes.ContentState
    let startedAt: Date

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            header
            if let rest = state.rest {
                resting(rest)
            } else {
                working
            }
            if let concern = state.concern {
                Label(concern, systemImage: "exclamationmark.triangle.fill")
                    .font(.caption)
                    .foregroundStyle(.orange)
                    .lineLimit(1)
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 12)
        .tint(.accentColor)
        .activitySystemActionForegroundColor(.accentColor)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(spoken)
    }

    private var header: some View {
        HStack(spacing: 8) {
            WorkoutGlyph(isResting: state.isResting)
                .font(.subheadline)
            Text(state.headline)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(.primary)
                .lineLimit(1)
            Spacer(minLength: 8)
            Text(state.setCountPhrase)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }

    /// The rest in the big type, with the elapsed clock demoted to a caption beside it.
    ///
    /// The bar is a `ProgressView(timerInterval:)` rather than a value: the system drains it, so it
    /// is still right after the app has been suspended for a minute.
    private func resting(_ rest: WorkoutActivityAttributes.Rest) -> some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline, spacing: 8) {
                WorkoutRestClock(rest: rest)
                    .font(.title.weight(.semibold))
                    .foregroundStyle(.primary)
                Text("rest")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
                Spacer(minLength: 8)
                // Trailing, because live timer text takes all the width it is offered and would
                // otherwise sit left-aligned in the middle of the row.
                WorkoutElapsedClock(startedAt: startedAt)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.trailing)
            }
            ProgressView(timerInterval: rest.interval, countsDown: true) {
                EmptyView()
            } currentValueLabel: {
                EmptyView()
            }
            .progressViewStyle(.linear)
        }
    }

    /// With no rest running, the session's own clock is the only figure worth the big type.
    private var working: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            WorkoutElapsedClock(startedAt: startedAt)
                .font(.title.weight(.semibold))
                .foregroundStyle(.primary)
            Text("elapsed")
                .font(.subheadline)
                .foregroundStyle(.secondary)
            Spacer(minLength: 0)
        }
    }

    /// Spoken as one phrase, because VoiceOver reading six separate labels off a Lock Screen is
    /// worse than one sentence. The clocks are deliberately absent: a system timer announces its
    /// own value, and repeating it here would read the countdown twice.
    private var spoken: String {
        var parts = [state.headline, state.setCountPhrase]
        if state.isResting { parts.append("resting") }
        if let concern = state.concern { parts.append(concern) }
        return parts.joined(separator: ", ")
    }
}
