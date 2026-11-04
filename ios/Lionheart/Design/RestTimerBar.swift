import SwiftUI

/// The rest between sets, counting down.
///
/// A custom view because a countdown with a progress track and two controls is three stock pieces
/// that have to agree about one instant, and `ProgressView(timerInterval:)` — which is the stock
/// answer — renders its own label and cannot carry the controls.
///
/// It ticks through `TimelineView(.periodic)`, so the second that changes redraws this bar and not
/// the set list it sits under. That is the difference between a smooth workout screen and one that
/// re-lays out forty rows every second.
///
/// It floats on glass rather than sitting in an opaque bar, because it comes and goes with the rest
/// between sets: chrome that appears over the list the athlete is reading should let that list show
/// through, which is what tells them the sets are still there underneath.
struct RestTimerBar: View {
    let rest: RestClock
    /// What the entry row is open on, so the rest says what it is a rest before: "Next 185 lb × 5".
    var next: String?
    /// The athlete's standard rest, which the clock's menu changes.
    var standard: TimeInterval
    let extend: (TimeInterval) -> Void
    let skip: () -> Void
    /// Makes a duration the standard rest and restarts this one at it.
    let choose: (TimeInterval) -> Void

    /// What "+" adds. A minute is too long to wait out and fifteen seconds is not worth a tap.
    private static let increment: TimeInterval = 30

    /// The rests the clock's menu offers, isolation work to heavy compounds.
    static let durations: [TimeInterval] = [60, 90, 120, 150, 180, 240, 300]

    /// The last stretch of a rest, when the clock turns and a soft tap says to get set.
    private static let closing: TimeInterval = 10

    var body: some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let remaining = rest.remaining(at: context.date)
            let isClosing = remaining > 0 && remaining <= Self.closing

            VStack(spacing: 4) {
                // Four things across one bar stop fitting at the accessibility text sizes, where
                // the alternative to a second line is every word broken mid-syllable — "Res/ting",
                // "Sk/ip". `ViewThatFits` picks by measuring rather than by a size threshold, so
                // it is right at whatever size the athlete has actually chosen.
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: Tokens.cardPadding) {
                        restingLabel
                        Spacer(minLength: 4)
                        clock(remaining, isClosing: isClosing)
                        controls
                    }

                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: Tokens.cardPadding) {
                            restingLabel
                            Spacer(minLength: 4)
                            clock(remaining, isClosing: isClosing)
                        }
                        HStack(spacing: Tokens.cardPadding) {
                            Spacer(minLength: 0)
                            controls
                        }
                    }
                }

                ProgressView(value: rest.fractionElapsed(at: context.date))
                    .tint(isClosing ? .orange : nil)
                    .accessibilityHidden(true)
            }
            // A soft tap as the last ten seconds start, so the athlete stands up before the alarm
            // rather than at it. Only while the screen is up, which is when it is being looked at.
            .sensoryFeedback(.impact(flexibility: .soft), trigger: isClosing) { was, now in !was && now }
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.vertical, 8)
            .glassChrome(in: .rect(cornerRadius: Tokens.chromeCornerRadius))
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.bottom, 6)
            // A bottom `safeAreaInset`'s content is handed the region that *includes* the home
            // indicator, so a floating card laid out in it runs off the screen and loses its
            // bottom corners. A bar with an opaque background wants exactly that bleed; a card
            // does not, and has to put the inset back itself.
            .safeAreaPadding(.bottom)
        }
    }

    private var restingLabel: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text("Resting")
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
            if let next {
                // An ideal width of nothing, so a long movement name never decides that the bar
                // needs two rows — that layout covers the Log button. It takes the room there is
                // and truncates.
                Text(next)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.label))
                    .lineLimit(1)
                    .frame(minWidth: 0, idealWidth: 0, maxWidth: .infinity, alignment: .leading)
            }
        }
        .accessibilityElement(children: .combine)
    }

    /// The countdown, and behind it the menu that sets how long a rest is: the clock is where the
    /// athlete looks when a rest feels wrong, so that is where changing it lives.
    private func clock(_ remaining: TimeInterval, isClosing: Bool) -> some View {
        Menu {
            Picker("Rest", selection: Binding(get: { standard }, set: choose)) {
                ForEach(Self.durations, id: \.self) { duration in
                    Text(Formatting.clock(seconds: duration)).tag(duration)
                }
            }
        } label: {
            Text(Formatting.clock(seconds: remaining))
                .font(.title3.monospacedDigit().weight(.semibold))
                .foregroundStyle(isClosing ? Color.orange : Color(.label))
                .contentTransition(.numericText(countsDown: true))
        }
        .accessibilityLabel("\(Int(remaining)) seconds of rest left")
        .accessibilityHint("Choose how long a rest is")
    }

    @ViewBuilder
    private var controls: some View {
        Button("+30s") { extend(Self.increment) }
            .buttonStyle(.bordered)
            .controlSize(.small)
            .accessibilityLabel("Add thirty seconds")

        Button("Skip", action: skip)
            .buttonStyle(.bordered)
            .controlSize(.small)
    }
}
