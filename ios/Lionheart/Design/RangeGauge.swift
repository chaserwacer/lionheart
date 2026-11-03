import SwiftUI

/// Where one value sits on a fixed scale, against the band the athlete usually sits in.
///
/// Drawn for Oura's contributor scores: a 0–100 track, his usual range shaded on it, his mean as a
/// tick, and the day's value as the mark. Comparing a score to his own spread is the reading the
/// number alone cannot give, and a list of twenty-five bare scores was the thing to replace. A
/// custom view because no stock control draws a value against a band: `Gauge` shows a value and
/// its bounds, not a reference range.
struct RangeGauge: View {
    let value: Double
    var scale: ClosedRange<Double> = 0...100
    /// The usual band, or `nil` with too little history to have one.
    var usual: ClosedRange<Double>?
    var mean: Double?
    /// The mark's colour, which carries the reading's standing.
    var mark: Color = Color(.label)

    @ScaledMetric(relativeTo: .caption) private var markSize: CGFloat = 9

    var body: some View {
        GeometryReader { proxy in
            let width = proxy.size.width
            let middle = proxy.size.height / 2
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(Color(.tertiarySystemFill))
                    .frame(height: 4)
                    .position(x: width / 2, y: middle)
                if let usual {
                    Capsule()
                        .fill(Color(.systemGray3))
                        .frame(width: max(4, x(usual.upperBound, width) - x(usual.lowerBound, width)), height: 8)
                        .position(x: (x(usual.lowerBound, width) + x(usual.upperBound, width)) / 2, y: middle)
                }
                if let mean {
                    Rectangle()
                        .fill(Color(.secondaryLabel))
                        .frame(width: 1.5, height: 12)
                        .position(x: x(mean, width), y: middle)
                }
                Circle()
                    .fill(mark)
                    .frame(width: markSize, height: markSize)
                    .position(x: x(value, width), y: middle)
            }
        }
        .accessibilityHidden(true)
    }

    private func x(_ point: Double, _ width: CGFloat) -> CGFloat {
        let span = scale.upperBound - scale.lowerBound
        let fraction = span > 0 ? (min(max(point, scale.lowerBound), scale.upperBound) - scale.lowerBound) / span : 0
        let inset = markSize / 2
        return inset + CGFloat(fraction) * (width - 2 * inset)
    }
}
