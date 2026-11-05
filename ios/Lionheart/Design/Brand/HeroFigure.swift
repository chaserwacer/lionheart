import SwiftUI

/// One figure as a hero carries it: a monospaced label naming it, the number in the wordmark's
/// heavy expanded face, and its unit or scale quieter beside it — "score 54 of 75", "sleep 6h 35m".
///
/// A custom view because the pairing recurs on every tab's hero and should read the same on each.
/// Several sit side by side in an `HStack` when a hero has more than one answer, so a second figure
/// is one more of these rather than a new layout. The number rolls to a new value rather than
/// cutting when `value` is given.
struct HeroFigure: View {
    let label: String
    let figure: String
    var unit: String?
    /// The figure as a number, for the rolling transition; text-only figures leave it out.
    var value: Double?
    var style: Font.TextStyle = .largeTitle
    /// The unit's colour: secondary, or a band's colour where the word beside the figure is a
    /// verdict.
    var unitTint: Color = Color(.secondaryLabel)

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(label)
                .font(.meta)
                .foregroundStyle(Color(.secondaryLabel))
                .lineLimit(1)
            HStack(alignment: .firstTextBaseline, spacing: 5) {
                Text(figure)
                    .font(.heroFigure(style))
                    .foregroundStyle(Color(.label))
                    .contentTransition(value.map { .numericText(value: $0) } ?? .identity)
                    .animation(reduceMotion ? nil : .snappy, value: value)
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
                if let unit {
                    Text(unit)
                        .font(.subheadline)
                        .foregroundStyle(unitTint)
                        .lineLimit(1)
                }
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label)
        .accessibilityValue([figure, unit].compactMap(\.self).joined(separator: " "))
    }
}
