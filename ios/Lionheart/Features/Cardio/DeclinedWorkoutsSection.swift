import SwiftUI

/// The ring's guesses in the span that its heart rate did not show as training, folded under one
/// line: how many, and each one with the heart rate that decided it when opened.
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
                LabeledContent("Ring detections left out") {
                    Text("\(declined.count)").monospacedDigit()
                }
            }
        } footer: {
            Text("Workouts the ring detected on its own count only when the heart rate shows training: 20 minutes at zone 2 or above, or 10 at zone 3 or above, for at least half the time read.")
        }
    }
}
