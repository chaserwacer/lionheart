import UIKit

/// The three moments in a workout worth feeling.
///
/// The moments a store or an action decides are here, so the app cannot drift into buzzing at
/// everything, which is how they stop meaning anything. The few that follow a change a view itself
/// shows — stepping Today's day, scrubbing a weekly chart past a week, a plain-words entry landing —
/// are SwiftUI's `.sensoryFeedback` on that view, tied to the value that changed. A confirmation
/// is a haptic *plus* the state visibly changing — never a toast for something the screen already
/// shows.
///
/// `UIFeedbackGenerator` is created per call rather than kept around. Apple's advice to prepare a
/// generator before a burst of feedback is for dragging and scrubbing; these three are single
/// discrete events seconds apart, and a retained generator would hold the Taptic Engine warm for
/// the length of a workout for no gain.
@MainActor
enum Haptics {
    /// A set landed. Light, because it happens dozens of times in a session.
    static func setLogged() {
        UIImpactFeedbackGenerator(style: .light).impactOccurred()
    }

    /// A rest ran out. The one haptic that arrives unprompted, so it is the firmest.
    static func restFinished() {
        UINotificationFeedbackGenerator().notificationOccurred(.success)
    }

    /// A record fell, or a workout was finished. Worth a flourish.
    static func achievement() {
        UIImpactFeedbackGenerator(style: .heavy).impactOccurred()
    }
}
