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
            // The countdown shares the label's line rather than taking `currentValueLabel`, which
            // draws it below the bar — past the bottom of the region, where the Island clips it.
            VStack(spacing: 4) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    Text("Resting")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    WorkoutRestClock(rest: rest)
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(.primary)
                        .multilineTextAlignment(.trailing)
                }
                ProgressView(timerInterval: rest.interval, countsDown: true) {
                    EmptyView()
                } currentValueLabel: {
                    EmptyView()
                }
                .progressViewStyle(.linear)
                .tint(.accentColor)
            }
        } else if let concern = state.concern {
            Label(concern, systemImage: "exclamationmark.triangle.fill")
                .font(.caption)
                .foregroundStyle(.orange)
                .lineLimit(1)
        }
    }
}
