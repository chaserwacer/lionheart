import SwiftUI

/// The handful of constants that are not Apple's.
///
/// Deliberately almost empty. The design system here *is* Apple's — system colours, system text
/// styles, stock containers — because the owner asked for an app that feels like Apple Music and
/// explicitly did not want the web app's custom look carried across. What lives here is Tempo's
/// palette and the two spacings stock SwiftUI has no name for.
///
/// The palette is accents and grounds, not a reskin: ordinary controls, text and backgrounds stay
/// the system's semantic colours. There is no colour literal anywhere in this app; each colour reads
/// the asset catalog, which is what makes light and dark correct for free.
enum Tokens {
    /// Brick, the one accent, applied once as `.tint` on `RootView` and inherited by everything. It
    /// is the mark's third bar, this week. Strong enough for controls and white text on it, but on
    /// Chalk about 4.3 to 1, so a mark, a fill or a large figure rather than small text there.
    static let accent = Color.accentColor

    /// The mark's other two bars: Graphite on a light ground, Chalk on a dark one.
    static let ink = Color(.ink)

    /// The palette's grey, for a chart's second series or reference line where a system grey would
    /// read as disabled.
    static let iron = Color(.iron)

    /// The brand's own ground — the launch screen, the opening and sign-in — in both appearances.
    static let graphite = Color(.graphite)

    /// Inset for content that sits inside a card rather than a `List` row.
    static let cardPadding: CGFloat = 16

    /// Corner radius for a piece of floating chrome too tall to be a capsule — the rest clock.
    /// Capsules need no number, so this is the only radius in the app.
    static let chromeCornerRadius: CGFloat = 22
}
