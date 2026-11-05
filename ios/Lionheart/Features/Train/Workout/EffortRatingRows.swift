import SwiftUI

/// The four effort ratings as `Form` rows, editing the ones the workout in flight holds.
///
/// A `Picker` in a `Form` renders as Apple's own value row, which gets Dynamic Type and VoiceOver
/// right and makes "not rated" an option rather than a thing to undo. Each pick is written to the
/// workout as it is made, so a relaunch keeps it, and the in-session sheet and the finish sheet
/// draw these same rows and so can never disagree about what was rated.
struct EffortRatingRows: View {
    let store: ActiveWorkoutStore

    var body: some View {
        ForEach(EffortScale.allCases, id: \.self) { scale in
            Picker(scale.label, selection: selection(scale)) {
                Text("Not rated").tag(Int?.none)
                ForEach(EffortScale.values, id: \.self) { value in
                    Text("\(value)").tag(Int?.some(value))
                }
            }
        }
    }

    /// Reads and writes the workout's own ratings, never a copy taken when the view was drawn, so
    /// two picks made before a redraw both land.
    private func selection(_ scale: EffortScale) -> Binding<Int?> {
        Binding(
            get: { store.workout?.effort[scale] },
            set: { value in store.rate((store.workout?.effort ?? .unrated).rating(scale, value)) })
    }
}

extension EffortScale {
    var label: String {
        switch self {
        case .accumulatedFatigue: "Accumulated fatigue"
        case .difficulty: "Difficulty"
        case .engagement: "Engagement"
        case .externalVariables: "Outside factors"
        }
    }

    /// The name in a one-line summary, where four long names would not fit beside their figures.
    var shortLabel: String {
        switch self {
        case .accumulatedFatigue: "Fatigue"
        case .difficulty: "Difficulty"
        case .engagement: "Engagement"
        case .externalVariables: "Outside"
        }
    }
}
