import SwiftUI

/// Tempo's two voices in type, carried from the lockup onto the pages: the wordmark's heavy expanded
/// SF Pro for a page's hero figure, and the tagline's monospaced face for the small labels that say
/// what a figure covers.
///
/// Both are the system font at a text style, so Dynamic Type scales them like any other text; only
/// `Wordmark` itself keeps a fixed size. Ordinary text — titles, rows, captions — stays the system's
/// default styles.
extension Font {
    /// A hero's figure — a score, a readiness, a night's sleep — at `style`, in the wordmark's
    /// weight and width, with digits that hold their width as the figure changes.
    static func heroFigure(_ style: Font.TextStyle = .largeTitle) -> Font {
        .system(style, weight: .heavy).width(.expanded).monospacedDigit()
    }

    /// What a figure covers — "7 days to today", "week 5 of 12" — in the tagline's monospaced face.
    static let meta = Font.caption.monospaced()
}
