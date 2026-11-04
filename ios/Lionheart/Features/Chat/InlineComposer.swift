import SwiftUI

/// The Chat tab's bottom row on iOS 26: the tab bar folded to the Chat glyph, and the composer
/// beside it. Asking is done in that row only; unfolding the tabs puts the composer away.
///
/// Drawn by the screen rather than asked of the system, because the system cannot be asked for it.
/// iOS 26 folds a tab bar only as the athlete scrolls (`tabBarMinimizeBehavior` has no "always"),
/// and the view it puts beside a folded bar, `tabViewBottomAccessory`, is one slot for the whole
/// `TabView` — the workout bar's, on every tab — whose placement follows the bar's fold rather than
/// setting it. So on Chat the system bar is hidden, and its folded form is drawn in glass at its
/// size, in the place it folds to, where tapping it unfolds the real one exactly as tapping a
/// folded bar does. Scrolling the transcript, or coming back to the tab, folds it again.
///
/// iOS 18 has no folded tab bar to match, so there the tabs stay as they are and the composer sits
/// above them.
struct InlineComposer: ViewModifier {
    /// Whether the athlete has unfolded the tab bar, which puts the composer away.
    @Binding var showsTabs: Bool

    @ViewBuilder
    func body(content: Content) -> some View {
        if Self.isAvailable {
            content
                .toolbarVisibility(showsTabs ? .visible : .hidden, for: .tabBar)
                .onScrollPhaseChange { _, phase in
                    if phase == .interacting, showsTabs { fold() }
                }
                .onDisappear { showsTabs = false }
        } else {
            content
        }
    }

    private func fold() {
        withAnimation(.snappy) { showsTabs = false }
    }

    /// Whether this OS folds tab bars, and so whether the composer sits beside one.
    static var isAvailable: Bool {
        if #available(iOS 26.0, *) { true } else { false }
    }
}

extension View {
    /// Puts the composer in one row with the folded tab bar, where the OS has one (`InlineComposer`).
    func composerInlineWithTabBar(showsTabs: Binding<Bool>) -> some View {
        modifier(InlineComposer(showsTabs: showsTabs))
    }
}

/// The tab bar, folded to the Chat tab's glyph, as the first piece of the composer's glass.
///
/// The glyph is the tab's own, outlined and tinted as the selected tab is, so it reads as the tab
/// bar and not as a new control. Tapping it unfolds the tabs.
///
/// A fixed size, as the system's tab bar is: it does not grow with Dynamic Type, which at the
/// accessibility sizes would take the room the field needs, and shows the large content viewer
/// instead, as a tab does.
struct FoldedTabBar: View {
    let unfold: () -> Void

    private static let diameter: CGFloat = 44

    var body: some View {
        Button(action: unfold) {
            Image(systemName: "bubble.left")
                .symbolVariant(.none)
                .font(.system(size: 17, weight: .semibold))
                .foregroundStyle(Tokens.accent)
                .frame(width: Self.diameter, height: Self.diameter)
                .glassChrome(in: .circle, interactive: true)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Tab bar")
        .accessibilityValue("Chat")
        .accessibilityHint("Shows the tabs")
        .accessibilityShowsLargeContentViewer {
            Label("Chat", systemImage: "bubble.left")
        }
    }
}
