import SwiftUI

/// One movement of a session as its own row — its name and its log-book line — which opens the
/// movement's page.
struct MovementLineRow: View {
    let digest: MovementDigest

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 0))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))

        layout {
            Text(digest.movementName)
                .font(.subheadline)
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
            Text(MovementLines.line(digest.line))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
                .fixedSize(horizontal: !dynamicTypeSize.isAccessibilitySize, vertical: false)
        }
        .accessibilityElement(children: .combine)
    }
}
