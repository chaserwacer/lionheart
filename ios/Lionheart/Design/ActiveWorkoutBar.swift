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
    /// Whether the slot holding the bar is the single-line one beside a minimized tab bar. The
    /// caller knows the placement; the bar only knows how much room it has.
    let isCondensed: Bool
    let open: () -> Void

    var body: some View {
        Button(action: open) {
            TimelineView(.periodic(from: .now, by: 1)) { context in
                HStack(spacing: 12) {
                    Image(systemName: icon(at: context.date))
                        .font(.title3)
                        .foregroundStyle(Tokens.accent)
                        .accessibilityHidden(true)

                    if isCondensed {
                        condensedLine(at: context.date)
                    } else {
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
                    }

                    Spacer(minLength: 8)

                    trailing(at: context.date)

                    if !isCondensed {
                        Image(systemName: "chevron.up")
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(Color(.tertiaryLabel))
                            .accessibilityHidden(true)
                    }
                }
                .padding(.horizontal, Tokens.cardPadding)
                .padding(.vertical, isCondensed ? 6 : 10)
                .contentShape(.rect)
                .accessibilityElement(children: .combine)
                .accessibilityLabel(spoken(at: context.date))
                .accessibilityHint("Opens the workout you are logging")
            }
        }
        .buttonStyle(.plain)
    }

    /// The one line the condensed slot has room for. Anything the athlete has to act on takes it;
    /// otherwise it is the movement, which is what they glance down for. Nothing is lost by the
    /// squeeze, because the spoken label is the same either way and the full bar is one scroll away.
    @ViewBuilder
    private func condensedLine(at now: Date) -> some View {
        if WorkoutAttention.isNeeded(for: workout, savedToDisk: isPersisted) {
            status(at: now)
                .font(.caption.weight(.semibold))
                .foregroundStyle(.orange)
                .lineLimit(1)
        } else {
            Text(headline)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(Color(.label))
                .lineLimit(1)
        }
    }

    /// The movement under the athlete's thumb, which is what they are looking for when they glance
    /// down. The fallback says what kind of workout it is, because a retro log has no clock worth
    /// reading and should not pretend to.
    private var headline: String {
        if let movementTitle, !movementTitle.isEmpty { return movementTitle }
        switch workout.destination {
        case .liveSession, .deferredStart: return "Workout in progress"
        case .afterTheFact: return "Logging a past workout"
        }
    }

    private func icon(at now: Date) -> String {
        workout.rest.isResting(at: now)
            ? "hourglass"
            : "record.circle"
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
            Text("No sets logged")
        } else if workout.workSetCount == 0 {
            Text("Warming up")
        } else {
            Text(Formatting.count(workout.workSetCount, "set"))
        }
    }

    /// The elapsed clock, or — for a workout being written down afterwards — the set count, since
    /// time since a start the athlete typed is not a number they want ticking at them.
    @ViewBuilder
    private func trailing(at now: Date) -> some View {
        switch workout.destination {
        case .liveSession, .deferredStart:
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
        if workout.workSetCount > 0 { parts.append(Formatting.count(workout.workSetCount, "set")) }
        if workout.destination.isLive {
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
/// returns to its root, which happens under the logging sheet either way.
///
/// The iOS 18 path keeps deciding inside the inset, where an `EmptyView` genuinely costs no height,
/// and supplies the floating glass chrome the iOS 26 slot provides for free.
struct WorkoutAccessory: ViewModifier {
    let workout: ActiveWorkout?
    let isPersisted: Bool
    let movementTitle: String?
    let open: () -> Void

    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            if let workout {
                content.tabViewBottomAccessory {
                    PlacedWorkoutBar(
                        workout: workout,
                        isPersisted: isPersisted,
                        movementTitle: movementTitle,
                        open: open)
                }
            } else {
                content
            }
        } else {
            content.safeAreaInset(edge: .bottom, spacing: 0) {
                if let workout {
                    ActiveWorkoutBar(
                        workout: workout,
                        isPersisted: isPersisted,
                        movementTitle: movementTitle,
                        isCondensed: false,
                        open: open)
                        .glassChrome(in: .capsule, interactive: true)
                        .padding(.horizontal, Tokens.cardPadding)
                        .padding(.bottom, 6)
                        // The inset's content region includes the home indicator, so a floating
                        // capsule laid out in it runs off the bottom of the screen. Seen and fixed
                        // on `RestTimerBar`, which is built the same way; this path has no iOS 18
                        // runtime to see it on, so it matches that one deliberately.
                        .safeAreaPadding(.bottom)
                }
            }
        }
    }
}

/// The bar, told how much room its slot has.
///
/// `tabViewBottomAccessory` hands the placement down through the environment, and it changes as the
/// tab bar minimizes: `.expanded` above a full tab bar, `.inline` on one line beside a minimized
/// one. Reading it here rather than in `ActiveWorkoutBar` keeps an iOS 26-only type out of `Design/`
/// and leaves the bar a function of how much space it has.
@available(iOS 26.0, *)
private struct PlacedWorkoutBar: View {
    let workout: ActiveWorkout
    let isPersisted: Bool
    let movementTitle: String?
    let open: () -> Void

    @Environment(\.tabViewBottomAccessoryPlacement) private var placement

    var body: some View {
        ActiveWorkoutBar(
            workout: workout,
            isPersisted: isPersisted,
            movementTitle: movementTitle,
            isCondensed: isCondensed,
            open: open)
    }

    /// No placement yet means the slot has not resolved one, which happens before the first layout;
    /// the roomier bar is the safer thing to draw then, because it is the one that fits an expanded
    /// slot and merely crops in an inline one.
    private var isCondensed: Bool {
        guard let placement else { return false }
        switch placement {
        case .inline: return true
        case .expanded: return false
        @unknown default: return false
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
