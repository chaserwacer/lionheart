import SwiftUI

/// One part of the score as the row that opens it: its name, its measurements in a line, and its
/// points.
struct ScorePartRow: View {
    let part: ScorePartBreakdown

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))
        layout {
            VStack(alignment: .leading, spacing: 2) {
                Text(part.part.name)
                    .font(.subheadline.weight(.semibold))
                Text(part.summary)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
            Text(ScorePoints.earned(part.points, of: part.outOf))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(part.points == nil ? Color(.secondaryLabel) : Color(.label))
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(part.part.name)
        .accessibilityValue("\(ScorePoints.spoken(part.points, of: part.outOf)). \(part.summary)")
    }
}
