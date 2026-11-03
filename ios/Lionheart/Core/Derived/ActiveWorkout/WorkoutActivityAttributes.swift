import ActivityKit
import Foundation

/// The workout as the Lock Screen and the Dynamic Island show it.
///
/// A second representation of the one piece of state the device owns, living in another process,
/// so its shape is chosen to make the two **unable** to disagree about the thing that matters most:
/// the clocks. Neither the elapsed time nor the rest countdown is a number in here. Both are
/// instants, and WidgetKit renders the counting itself from a date range — which is why a suspended
/// app still shows a correct countdown, and why nothing has to be pushed once a second.
///
/// Everything else is a flat, already-decided value. The extension makes no decision and reaches
/// for no store: resolving a movement id to a name, choosing which concern is worth saying, and
/// deciding whether a rest is worth showing at all happen in the app, where the workout is.
///
/// `ActivityKit` is imported in `Core/` because `ActivityAttributes` is a data protocol —
/// `Codable & Hashable` and nothing more. `check-layering.sh` bars the renderers (SwiftUI, UIKit,
/// Charts) and this is not one; the type is a value and its derivation is unit-tested without a
/// host, which is the property that rule protects.
struct WorkoutActivityAttributes: ActivityAttributes, Equatable, Sendable {
    /// The live session this activity mirrors.
    ///
    /// Fixed for the activity's life, and the whole of how a stale activity is reconciled at
    /// startup: ActivityKit activities outlive the app, so the next launch compares this against
    /// the session restored from disk and either adopts the activity or ends it. Without an
    /// identity on the attributes there is no way to tell "my workout's activity, still running"
    /// from "the last workout's activity, orphaned".
    let sessionId: UUID

    /// When the server started the session.
    ///
    /// An attribute rather than part of the content because it never changes — a live session's
    /// start is the server's, stamped when it created the session — and because it is the anchor
    /// the elapsed clock counts up from. Only a live session gets an activity at all; a workout
    /// being written down afterwards has no clock worth reading, and the athlete is holding the
    /// phone while they type it.
    let startedAt: Date

    /// What changes while the workout runs.
    ///
    /// Hashable is ActivityKit's requirement, and it earns its keep twice: the app compares the
    /// content it is about to send against the content already showing and skips an update that
    /// would say the same thing. Logging ten sets against one movement is one activity update, not
    /// ten, without the store knowing anything about how often it is called.
    struct ContentState: Codable, Hashable, Sendable {
        /// The movement under the athlete's thumb, resolved to a name by the app. `nil` before the
        /// first movement is chosen, which the views answer with the kind of workout rather than a
        /// blank line.
        let movementTitle: String?

        let setCount: Int

        /// The rest in flight, or `nil` when the athlete is working.
        ///
        /// Absence is right here rather than a case: the views render a resting workout and a
        /// working one as two different things, and there is no third.
        let rest: Rest?

        /// The one thing the athlete has to act on, in the app's own words, or `nil` when there is
        /// nothing to say.
        ///
        /// A resolved string rather than the sync state, because choosing what to say about an
        /// unsynced workout is a decision and decisions do not belong in the extension. It also
        /// keeps the Lock Screen and the in-app bar saying the same sentence.
        let concern: String?

        /// When this content stops being true without the app saying so: the end of the rest.
        ///
        /// A rest that runs out while the app is suspended has nobody awake to take it off the
        /// Lock Screen, so the countdown would sit at zero until the athlete opened the app.
        /// Handed to ActivityKit as the stale date, it makes the system draw the activity again at
        /// that instant, and `showing(isStale:)` is what that drawing reads.
        var staleDate: Date? { rest?.endsAt }

        /// The content as the activity should draw it.
        ///
        /// Once stale, the rest it carries has run out, so it reads as working — the elapsed clock
        /// and the lifting glyph — exactly as it will once the app retires the rest itself.
        func showing(isStale: Bool) -> Self {
            guard isStale, rest != nil else { return self }
            return Self(movementTitle: movementTitle, setCount: setCount, rest: nil, concern: concern)
        }
    }

    /// A rest as the system can count it down.
    ///
    /// Two instants rather than `RestClock`'s start-and-duration, because that is what
    /// `Text(timerInterval:)` and `ProgressView(timerInterval:)` take. The conversion happens once,
    /// here, at the boundary — and it is the reason `init?` is failable: `startedAt...endsAt` is a
    /// `ClosedRange`, which **traps** when its upper bound is below its lower one, so a rest of no
    /// length has to be refused rather than built.
    struct Rest: Codable, Hashable, Sendable {
        let startedAt: Date
        let endsAt: Date

        init?(startedAt: Date, endsAt: Date) {
            guard endsAt > startedAt else { return nil }
            self.startedAt = startedAt
            self.endsAt = endsAt
        }

        /// The range the system counts over. Safe by construction: `init?` refused the empty case.
        var interval: ClosedRange<Date> { startedAt...endsAt }
    }
}
