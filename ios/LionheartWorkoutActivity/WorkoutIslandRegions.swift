import SwiftUI

/// What the expanded Dynamic Island puts in its leading region: what the athlete is doing.
///
/// The Island is always dark, whatever the phone's appearance, so these views use `.primary` and
/// `.secondary` and let the system resolve them rather than naming a colour.
struct WorkoutIslandLeading: View {
    let state: WorkoutActivityAttributes.ContentState

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: 6) {
                WorkoutGlyph(isResting: state.isResting)
                    .font(.caption)
                Text(state.headline)
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(.primary)
                    .lineLimit(1)
            }
            Text(state.setCountPhrase)
                .font(.caption)
                .foregroundStyle(.secondary)
        }
    }
}

/// What the expanded Island puts across its bottom: the rest, or the one concern, or nothing.
///
/// Nothing is a real answer. An expanded Island whose bottom region is empty closes up rather than
/// drawing a blank strip, and a workout between sets with everything synced has nothing more to say
/// than the two regions above it already do.
struct WorkoutIslandBottom: View {
    let state: WorkoutActivityAttributes.ContentState

    var body: some View {
        if let rest = state.rest {
            ProgressView(timerInterval: rest.interval, countsDown: true) {
                Text("Resting")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            } currentValueLabel: {
                WorkoutRestClock(rest: rest)
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.primary)
            }
            .progressViewStyle(.linear)
            .tint(.accentColor)
        } else if let concern = state.concern {
            Label(concern, systemImage: "exclamationmark.triangle.fill")
                .font(.caption)
                .foregroundStyle(.orange)
                .lineLimit(1)
        }
    }
}
