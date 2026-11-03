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
    let extend: (TimeInterval) -> Void
    let skip: () -> Void

    /// What "+" adds. A minute is too long to wait out and fifteen seconds is not worth a tap.
    private static let increment: TimeInterval = 30

    var body: some View {
        TimelineView(.periodic(from: .now, by: 1)) { context in
            let remaining = rest.remaining(at: context.date)

            VStack(spacing: 6) {
                // Four things across one bar stop fitting at the accessibility text sizes, where
                // the alternative to a second line is every word broken mid-syllable — "Res/ting",
                // "Sk/ip". `ViewThatFits` picks by measuring rather than by a size threshold, so
                // it is right at whatever size the athlete has actually chosen.
                ViewThatFits(in: .horizontal) {
                    HStack(spacing: Tokens.cardPadding) {
                        restingLabel
                        Spacer(minLength: 4)
                        clock(remaining)
                        controls
                    }

                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: Tokens.cardPadding) {
                            restingLabel
                            Spacer(minLength: 4)
                            clock(remaining)
                        }
                        HStack(spacing: Tokens.cardPadding) {
                            Spacer(minLength: 0)
                            controls
                        }
                    }
                }

                ProgressView(value: rest.fractionElapsed(at: context.date))
                    .accessibilityHidden(true)
            }
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.vertical, 10)
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
        Label("Resting", systemImage: "hourglass")
            .font(.subheadline)
            .foregroundStyle(Color(.secondaryLabel))
            .labelStyle(.titleAndIcon)
    }

    private func clock(_ remaining: TimeInterval) -> some View {
        Text(Formatting.clock(seconds: remaining))
            .font(.title3.monospacedDigit().weight(.semibold))
            .foregroundStyle(Color(.label))
            .accessibilityLabel("\(Int(remaining)) seconds of rest left")
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
