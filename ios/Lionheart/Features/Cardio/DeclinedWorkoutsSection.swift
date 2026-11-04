import SwiftUI

/// The ring's guesses in the span, and workouts nobody named, that their heart rate did not show
/// as training, folded under one line: how many, and each one with the heart rate that decided it.
///
/// Folded because a span holds dozens of short detected walks and games of catch; listed at all so
/// the call is there to check rather than taken on trust.
struct DeclinedWorkoutsSection: View {
    let declined: [DeclinedWorkout]
    let span: TrendSpan

    var body: some View {
        Section {
            DisclosureGroup {
                ForEach(declined.reversed()) { workout in
                    NavigationLink(value: BodyRoute.cardioSession(workout.session.id, span)) {
                        DeclinedWorkoutRow(declined: workout)
                    }
                }
            } label: {
                LabeledContent("Left out by heart rate") {
                    Text("\(declined.count)").monospacedDigit()
                }
            }
        } footer: {
            Text(CardioWords.evidenceRule)
        }
    }
}
