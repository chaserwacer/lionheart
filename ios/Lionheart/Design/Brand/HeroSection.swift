import SwiftUI

/// A tab's hero: the one `List` section that carries the page's most important answer, drawn on
/// Graphite — the brand's own ground, as on the launch screen and sign-in — in both appearances
/// (`Tokens.heroGround`, lifted a step on a dark page so it still stands out).
///
/// Still a `Section` of ordinary rows, so separators, navigation links, swipe targets and Dynamic
/// Type behave as on every other section; only the ground and the scheme change. Inside it the rows
/// read the dark appearance, so `Tokens.ink` is Chalk, the accent is the lighter Brick and the
/// semantic label colours stay legible on the dark ground. One per tab, so it stays an emphasis.
struct HeroSection<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        Section {
            content
                .listRowBackground(Tokens.heroGround)
                .listRowSeparatorTint(Tokens.iron.opacity(0.45))
                .environment(\.colorScheme, .dark)
        }
    }
}
