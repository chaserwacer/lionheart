import SwiftUI

/// The handful of constants that are not Apple's.
///
/// Deliberately almost empty. The design system here *is* Apple's — system colours, system text
/// styles, stock containers — because the owner asked for an app that feels like Apple Music and
/// explicitly did not want the web app's custom look carried across. What lives here is the one
/// brand token and the two spacings that stock SwiftUI has no name for.
///
/// There is no colour literal anywhere in this app. `accent` reads the asset catalog, which is what
/// makes light and dark correct for free; every other colour comes from a semantic system value.
enum Tokens {
    /// The one brand colour, applied once as `.tint` on `RootView` and inherited by everything.
    static let accent = Color.accentColor

    /// Inset for content that sits inside a card rather than a `List` row.
    static let cardPadding: CGFloat = 16
}
