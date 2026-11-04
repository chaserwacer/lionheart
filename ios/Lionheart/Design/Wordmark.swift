import SwiftUI

/// "tempo", lowercase, heavy and tightly tracked — the one place this app sets its own type.
///
/// SF Pro rather than a bundled face: Heavy at the expanded width carries the approved Archivo 800
/// lockup closely enough that a font file would add weight and no character. A size rather than a
/// text style, because a wordmark is a picture of a name and keeps its proportions; VoiceOver reads
/// it as the name.
struct Wordmark: View {
    var size: CGFloat = 44

    var body: some View {
        Text(verbatim: "tempo")
            .font(.system(size: size, weight: .heavy).width(.expanded))
            .tracking(-size * 0.04)
            .foregroundStyle(Tokens.ink)
            .lineLimit(1)
            .fixedSize()
            .accessibilityLabel("Tempo")
    }
}
