import SwiftUI

/// The mark in its colours: the first two bars in ink, the third — this week — in Brick.
///
/// Ink and Brick both follow the appearance, so on a Graphite ground (forced dark) the pair is Chalk
/// and this week the lighter Brick, as on the launch screen. Hidden from VoiceOver: wherever it
/// stands, the words beside it say the name.
struct BrandMark: View {
    /// Each bar's vertical offset from its place, for the opening, where the bars rise one after
    /// another from the launch screen's mark into the lockup.
    var offsets: [CGFloat] = [0, 0, 0]
    /// How far each bar has grown from the baseline, 0 to 1, for the loading indicator.
    var rises: [Double] = [1, 1, 1]

    var body: some View {
        ZStack {
            ForEach(ProgressionMark.Bar.allCases, id: \.self) { bar in
                ProgressionMark(bars: [bar], rise: rises[bar.rawValue])
                    .fill(bar == .third ? Tokens.accent : Tokens.ink)
                    .offset(y: offsets[bar.rawValue])
            }
        }
        .aspectRatio(ProgressionMark.aspectRatio, contentMode: .fit)
        .accessibilityHidden(true)
    }
}
