import SwiftUI

/// The app's one glass treatment: real Liquid Glass on iOS 26, a vibrancy material below it.
///
/// It exists so the availability check is written **here** and not at every bar. Both branches draw
/// the same shape, so there is one piece of chrome to design and one to fall back to rather than two
/// designs that drift apart.
///
/// Glass is chrome, not decoration. It goes on a bar that floats over content the athlete is
/// scrolling — which is where Apple puts it — and on nothing else; a card, a row and a section
/// header stay as `List` draws them. That is the design-system rule rather than a preference.
///
/// The iOS 18 branch is deliberately the plain one. No iOS 18 runtime is installed on this machine,
/// so it is compile-verified and has never been seen running; `.regularMaterial` is the documented
/// vibrancy material for a floating panel and adapts to both appearances by itself, which is a
/// property a reader can check without a device.
private struct GlassChrome<ChromeShape: Shape>: ViewModifier {
    let shape: ChromeShape
    /// Whether the whole surface is itself a control, so the glass lenses under the finger as it
    /// does in Apple's own chrome. Only the workout bar is.
    let isInteractive: Bool

    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            content.glassEffect(.regular.interactive(isInteractive), in: shape)
        } else {
            content.background(.regularMaterial, in: shape)
        }
    }
}

extension View {
    /// Floating chrome: a bar or control that sits over scrolling content, inset from the edges it
    /// floats above.
    ///
    /// Untinted, because the one brand token in this app is the `.tint` applied on `RootView` and
    /// tinted glass would make the chrome the loudest thing on the screen. The glyphs and controls
    /// inside it inherit the tint; the glass itself stays the system's.
    func glassChrome(in shape: some Shape = .capsule, interactive: Bool = false) -> some View {
        modifier(GlassChrome(shape: shape, isInteractive: interactive))
    }
}

/// Sibling glass surfaces drawn as one piece of glass, so they bend light together and merge as
/// they approach instead of cross-fading two separate sheets.
///
/// A plain passthrough below iOS 26, where there is no glass to join — which is why the grouping
/// lives in a container rather than in `glassChrome(in:interactive:)`: the siblings are the caller's
/// arrangement, and only the caller knows which ones belong to the same piece.
struct GlassChromeGroup<Content: View>: View {
    private let spacing: CGFloat?
    private let content: Content

    init(spacing: CGFloat? = nil, @ViewBuilder content: () -> Content) {
        self.spacing = spacing
        self.content = content()
    }

    @ViewBuilder
    var body: some View {
        if #available(iOS 26.0, *) {
            GlassEffectContainer(spacing: spacing) { content }
        } else {
            content
        }
    }
}
