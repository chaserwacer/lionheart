import SwiftUI

/// A measured quantity against its target: a filled bar on a track, with the target as a tick.
///
/// For any "this much of that much" reading — hard sets against a weekly target, minutes of zone 2
/// against 150 — where the number alone does not show how close it is and a percentage would hide
/// the units. The scale runs half again past the target, so a week over it reads as over rather
/// than as a full bar. A custom view because `Gauge`'s linear style draws a value inside its bounds
/// and has no mark for a target inside the range.
struct TargetBar: View {
    let value: Double
    let target: Double

    /// Grey under the target and green once it is met: the bar's one job is to say which side of
    /// the tick the week is on, and the semantic green says "met" without a word.
    private var fill: Color { value >= target && target > 0 ? .green : Color(.systemGray) }

    @ScaledMetric(relativeTo: .caption) private var thickness: CGFloat = 6

    private var scale: Double { max(target * 1.5, value, 1) }

    var body: some View {
        GeometryReader { proxy in
            let width = proxy.size.width
            ZStack(alignment: .leading) {
                Capsule()
                    .fill(Color(.tertiarySystemFill))
                    .frame(height: thickness)
                Capsule()
                    .fill(fill)
                    .frame(width: value > 0 ? max(thickness, width * value / scale) : 0, height: thickness)
                if target > 0 {
                    Rectangle()
                        .fill(Color(.label))
                        .frame(width: 1.5, height: thickness * 2)
                        .offset(x: width * target / scale - 0.75)
                }
            }
            .frame(maxHeight: .infinity)
        }
        .frame(height: thickness * 2)
        .accessibilityHidden(true)
    }
}
