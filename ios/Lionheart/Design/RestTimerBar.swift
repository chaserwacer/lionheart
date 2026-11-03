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
                HStack(spacing: Tokens.cardPadding) {
                    Label("Resting", systemImage: "hourglass")
                        .font(.subheadline)
                        .foregroundStyle(Color(.secondaryLabel))
                        .labelStyle(.titleAndIcon)

                    Spacer(minLength: 4)

                    Text(Formatting.clock(seconds: remaining))
                        .font(.title3.monospacedDigit().weight(.semibold))
                        .foregroundStyle(Color(.label))
                        .accessibilityLabel("\(Int(remaining)) seconds of rest left")

                    Button("+30s") { extend(Self.increment) }
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                        .accessibilityLabel("Add thirty seconds")

                    Button("Skip", action: skip)
                        .buttonStyle(.bordered)
                        .controlSize(.small)
                }

                ProgressView(value: rest.fractionElapsed(at: context.date))
                    .accessibilityHidden(true)
            }
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.vertical, 10)
        }
    }
}
