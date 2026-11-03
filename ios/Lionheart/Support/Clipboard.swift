import UIKit

/// Putting something on the pasteboard.
///
/// One place, so `UIKit` stays out of the screens. Nothing is shown afterwards: every app on the
/// platform copies silently from a context menu, and a banner for it would be a toast for an
/// action the athlete just chose by name.
@MainActor
enum Clipboard {
    static func copy(_ text: String) {
        UIPasteboard.general.string = text
    }
}
