import SwiftUI

/// This app's now-playing bar: the workout in progress, above the tab bar, on every tab.
///
/// A custom view because SwiftUI has no stock equivalent — the accessory slot takes content, not a
/// configured control. It is kept to system materials, system text styles and the app tint so it
/// reads as Apple's own, and it is **one** view rendered by both OS paths (`tabViewBottomAccessory`
/// on iOS 26, `safeAreaInset` on iOS 18) so there is one appearance to design and one to maintain.
///
/// The elapsed time ticks through `TimelineView(.periodic)`, so the clock redraws and nothing else
/// does. Once the set list exists above it, that distinction is what keeps a one-second tick from
/// redrawing every row.
struct ActiveWorkoutBar: View {
    let workout: ActiveWorkout
    let isPersisted: Bool
    let open: () -> Void

    var body: some View {
        Button(action: open) {
            HStack(spacing: 12) {
                Image(systemName: "figure.strengthtraining.traditional")
                    .font(.title3)
                    .foregroundStyle(Tokens.accent)
                    .accessibilityHidden(true)

                VStack(alignment: .leading, spacing: 1) {
                    Text("Workout in progress")
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(Color(.label))
                    status
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }

                Spacer(minLength: 8)

                TimelineView(.periodic(from: .now, by: 1)) { context in
                    Text(Formatting.clock(seconds: workout.elapsed(at: context.date)))
                        .font(.body.monospacedDigit())
                        .foregroundStyle(Color(.label))
                }

                Image(systemName: "chevron.up")
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(Color(.tertiaryLabel))
                    .accessibilityHidden(true)
            }
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.vertical, 10)
            .contentShape(.rect)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Workout in progress")
        .accessibilityHint("Opens the workout you are logging")
    }

    /// One line about where the workout stands. The unsaved case comes first because it is the only
    /// one the athlete has to act on.
    @ViewBuilder
    private var status: some View {
        if !isPersisted {
            Label("Not saved to this device", systemImage: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
        } else if !workout.isSynced {
            Label("Waiting to sync", systemImage: "arrow.triangle.2.circlepath")
        } else {
            Text("Tap to log sets")
        }
    }
}

/// Hangs the workout bar on a `TabView` as a persistent accessory.
///
/// Two OS paths, one view. The modifier is applied **unconditionally** and the emptiness decided
/// inside its content: making the modifier itself conditional would change the `TabView`'s view
/// identity the moment a workout starts, and SwiftUI would reset the selected tab underneath the
/// athlete.
struct WorkoutAccessory: ViewModifier {
    let workout: ActiveWorkout?
    let isPersisted: Bool
    let open: () -> Void

    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            content.tabViewBottomAccessory { bar }
        } else {
            content.safeAreaInset(edge: .bottom, spacing: 0) {
                // On iOS 18 the bar supplies its own background, which the iOS 26 accessory slot
                // provides for it. An EmptyView here insets by nothing, so no workout costs no
                // space.
                if workout != nil {
                    bar
                        .background(.bar)
                        .overlay(alignment: .top) { Divider() }
                }
            }
        }
    }

    @ViewBuilder
    private var bar: some View {
        if let workout {
            ActiveWorkoutBar(workout: workout, isPersisted: isPersisted, open: open)
        }
    }
}

extension View {
    /// The workout bar, as the skill describes it: visible from every tab, gone when nothing is
    /// in flight.
    func workoutAccessory(
        _ workout: ActiveWorkout?, isPersisted: Bool, open: @escaping () -> Void
    ) -> some View {
        modifier(WorkoutAccessory(workout: workout, isPersisted: isPersisted, open: open))
    }
}
