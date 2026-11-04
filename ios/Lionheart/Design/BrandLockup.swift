import SwiftUI

/// The mark over the wordmark over the tagline: the opening and sign-in draw the same one, so the
/// opening can end exactly on sign-in's.
///
/// The mark stands in a square of `markSide`, the size the launch screen's image is drawn at, so
/// the opening's first frame is the launch screen's.
struct BrandLockup: View {
    /// Each bar's offset from its place; see `BrandMark.offsets`.
    var barOffsets: [CGFloat] = [0, 0, 0]
    var showsWordmark = true
    var showsTagline = true

    static let markSide: CGFloat = 100

    var body: some View {
        VStack(spacing: 0) {
            BrandMark(offsets: barOffsets)
                .frame(width: Self.markSide, height: Self.markSide)
            Wordmark()
                .padding(.top, 10)
                .opacity(showsWordmark ? 1 : 0)
                .offset(y: showsWordmark ? 0 : 6)
            Tagline()
                .padding(.top, 2)
                .opacity(showsTagline ? 1 : 0)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Tempo. Keep moving forward.")
    }
}

/// "keep moving forward." — lowercase, monospaced, with its full stop.
private struct Tagline: View {
    var body: some View {
        Text(verbatim: "keep moving forward.")
            .font(.system(size: 13, design: .monospaced))
            .foregroundStyle(Color(.secondaryLabel))
            .lineLimit(1)
            .fixedSize()
    }
}
