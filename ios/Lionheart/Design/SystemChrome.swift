import SwiftUI

/// Stock chrome behaviours that live above the iOS 18 floor, each wrapped once so the availability
/// check is not written at the screen.
///
/// Nothing here draws anything. These are Apple's own behaviours — the tab bar that shrinks as you
/// scroll, the search field that collapses into the toolbar — turned on where this app wants them
/// and absent below iOS 26, where the system bars do not do either. Wrapping them matters because
/// the alternative is an `if #available` around a whole `TabView` or `List`, which means
/// maintaining the screen twice.

/// Shrinks the tab bar as the athlete scrolls down, and restores it when they scroll back.
///
/// What Music and the App Store do: the chrome gets out of the way and the content fills the screen.
/// It also decides the workout accessory's slot — a minimized tab bar puts that bar on one line
/// beside it — so the two belong to one gesture and are read together in `PlacedWorkoutBar`.
private struct TabBarMinimizing: ViewModifier {
    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            content.tabBarMinimizeBehavior(.onScrollDown)
        } else {
            content
        }
    }
}

/// Collapses the search field into the navigation bar until it is tapped.
///
/// Right where search is the occasional question about a screen whose own content is the answer —
/// the session log — and wrong where finding something is the only reason the screen opened, which
/// is why the movement picker keeps its field in view.
private struct SearchMinimizing: ViewModifier {
    @ViewBuilder
    func body(content: Content) -> some View {
        if #available(iOS 26.0, *) {
            content.searchToolbarBehavior(.minimize)
        } else {
            content
        }
    }
}

extension View {
    /// The current Apple tab-bar behaviour: minimize on scroll down, restore on scroll up.
    func minimizingTabBarOnScroll() -> some View {
        modifier(TabBarMinimizing())
    }

    /// Puts the search field in the navigation bar as a button until it is wanted.
    func minimizingSearchField() -> some View {
        modifier(SearchMinimizing())
    }
}
