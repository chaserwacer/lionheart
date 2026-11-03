import SwiftUI

/// This app's now-playing bar: the workout in progress, above the tab bar, on every tab.
///
/// A custom view because SwiftUI has no stock equivalent — the accessory slot takes content, not a
/// configured control. It is kept to system materials, system text styles and the app tint so it
/// reads as Apple's own, and it is **one** view rendered by both OS paths (`tabViewBottomAccessory`
/// on iOS 26, `safeAreaInset` on iOS 18) so there is one appearance to design and one to maintain.
///
/// Everything that changes with the second — the elapsed clock and the rest countdown — is inside a
/// single `TimelineView(.periodic)`, so a tick redraws this bar and nothing above it. With a set
/// list on screen that distinction is the difference between a usable screen and one that re-lays
/// out forty rows a second.
struct ActiveWorkoutBar: View {
    let workout: ActiveWorkout
    let isPersisted: Bool
    /// The movement being worked through, resolved by the caller: the bar is in `Design/` and does
    /// not reach for a store to turn an id into a name.
    let movementTitle: String?
    let open: () -> Void

    var body: some View {
        Button(action: open) {
            TimelineView(.periodic(from: .now, by: 1)) { context in
                HStack(spacing: 12) {
                    Image(systemName: icon(at: context.date))
                        .font(.title3)
                        .foregroundStyle(Tokens.accent)
                        .accessibilityHidden(true)

                    VStack(alignment: .leading, spacing: 1) {
                        Text(headline)
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(Color(.label))
                            .lineLimit(1)
                        status(at: context.date)
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                            .lineLimit(1)
                    }

                    Spacer(minLength: 8)

                    trailing(at: context.date)

                    Image(systemName: "chevron.up")
                        .font(.caption.weight(.semibold))
                        .foregroundStyle(Color(.tertiaryLabel))
                        .accessibilityHidden(true)
                }
                .padding(.horizontal, Tokens.cardPadding)
                .padding(.vertical, 10)
                .contentShape(.rect)
                .accessibilityElement(children: .combine)
                .accessibilityLabel(spoken(at: context.date))
                .accessibilityHint("Opens the workout you are logging")
            }
        }
        .buttonStyle(.plain)
    }

    /// The movement under the athlete's thumb, which is what they are looking for when they glance
    /// down. The fallback says what kind of workout it is, because a retro log has no clock worth
    /// reading and should not pretend to.
    private var headline: String {
        if let movementTitle, !movementTitle.isEmpty { return movementTitle }
        switch workout.destination {
        case .liveSession: return "Workout in progress"
        case .afterTheFact: return "Logging a past workout"
        }
    }

    private func icon(at now: Date) -> String {
        workout.rest.isResting(at: now)
            ? "hourglass"
            : "figure.strengthtraining.traditional"
    }

    /// One line about where the workout stands, most pressing first.
    ///
    /// The unsaved case leads because it is the only one the athlete has to act on, and a sync
    /// problem comes before the rest clock for the same reason. `.pending` says nothing: almost
    /// every set is pending for a second, and warning about it would teach the athlete to ignore
    /// the line.
    @ViewBuilder
    private func status(at now: Date) -> some View {
        if !isPersisted {
            Label("Not saved to this device", systemImage: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
        } else if case .refused(let reason) = workout.sync {
            Label(reason, systemImage: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
        } else if case .failed = workout.sync {
            Label("Waiting to sync", systemImage: "arrow.triangle.2.circlepath")
        } else if workout.rest.isResting(at: now) {
            Text("Resting \(Formatting.clock(seconds: workout.rest.remaining(at: now)))")
                .monospacedDigit()
        } else if workout.sets.isEmpty {
            Text("Tap to log sets")
        } else {
            Text(Formatting.count(workout.sets.count, "set"))
        }
    }

    /// The elapsed clock, or — for a workout being written down afterwards — the set count, since
    /// time since a start the athlete typed is not a number they want ticking at them.
    @ViewBuilder
    private func trailing(at now: Date) -> some View {
        switch workout.destination {
        case .liveSession:
            Text(Formatting.clock(seconds: workout.elapsed(at: now)))
                .font(.body.monospacedDigit())
                .foregroundStyle(Color(.label))
        case .afterTheFact:
            Image(systemName: "square.and.pencil")
                .font(.body)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private func spoken(at now: Date) -> String {
        var parts = [headline]
        if !isPersisted {
            parts.append("not saved to this device")
        } else if case .refused(let reason) = workout.sync {
            parts.append(reason)
        } else if case .failed = workout.sync {
            parts.append("waiting to sync")
        }
        if workout.rest.isResting(at: now) {
            parts.append("resting, \(Int(workout.rest.remaining(at: now))) seconds left")
        }
        if !workout.sets.isEmpty { parts.append(Formatting.count(workout.sets.count, "set")) }
        if case .liveSession = workout.destination {
            parts.append("\(Formatting.duration(seconds: workout.elapsed(at: now))) elapsed")
        }
        return parts.joined(separator: ", ")
    }
}

/// Hangs the workout bar on a `TabView` as a persistent accessory.
///
/// Two OS paths, one view.
///
/// On iOS 26 the modifier is applied **only while a workout is in flight**, which was learned by
/// looking: the accessory slot draws its own glass capsule whatever is inside it, so supplying an
/// `EmptyView` leaves an empty pill floating above the tab bar on every screen of the app. The
/// condition costs a `TabView` rebuild when a workout starts or ends — the selected tab survives
/// it, because the selection is `@State` outside the `TabView` — and each tab's navigation stack
/// returns to its root, which happens under a full-screen cover either way.
///
/// The iOS 18 path keeps deciding inside the inset, where an `EmptyView` genuinely costs no height.
struct WorkoutAccessory: ViewModifier {
    let workout: ActiveWorkout?
    let isPersisted: Bool
    let movementTitle: String?
    let open: () -> Void

    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            if workout == nil {
                content
            } else {
                content.tabViewBottomAccessory { bar }
            }
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
            ActiveWorkoutBar(
                workout: workout,
                isPersisted: isPersisted,
                movementTitle: movementTitle,
                open: open)
        }
    }
}

extension View {
    /// The workout bar, as the skill describes it: visible from every tab, gone when nothing is
    /// in flight.
    func workoutAccessory(
        _ workout: ActiveWorkout?,
        isPersisted: Bool,
        movementTitle: String?,
        open: @escaping () -> Void
    ) -> some View {
        modifier(WorkoutAccessory(
            workout: workout, isPersisted: isPersisted, movementTitle: movementTitle, open: open))
    }
}
