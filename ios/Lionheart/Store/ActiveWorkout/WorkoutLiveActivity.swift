import ActivityKit
import Foundation

/// The real ActivityKit side of the workout's Lock Screen presence.
///
/// It owns one handle and treats ActivityKit as the unreliable collaborator it is: the athlete can
/// turn Live Activities off in Settings, the system caps how many may run, and `request` throws for
/// reasons the app cannot do anything about. **None of that is a failure the athlete hears about.**
/// The Lock Screen is a mirror, never the record — the record is on disk and on the server — so
/// every path here is best-effort and silent, and nothing it does can make a set fail to log.
///
/// It is a class rather than a value because it has identity: the activity handle is the one thing
/// in this app that outlives the process that created it.
@MainActor
final class WorkoutLiveActivity: WorkoutActivityPresenting {
    /// The activity this launch is driving, once there is one.
    ///
    /// Also set by adopting an activity found at startup, which is why requesting one lazily is not
    /// enough: an activity from a previous launch is still on screen and still ours, and requesting
    /// a second would leave two workouts on the Lock Screen.
    private var showing: LiveActivityHandle?

    /// The content last handed to ActivityKit.
    ///
    /// The whole reason the store can call `present` on every mutation without becoming an update
    /// pump: logging ten sets against one movement derives ten identical content states, and nine
    /// of them stop here. `ContentState` is `Hashable` precisely so this comparison is free.
    private var shown: WorkoutActivityAttributes.ContentState?

    func present(_ projection: WorkoutActivityProjection?) async {
        guard let projection else {
            await endEverything()
            return
        }

        // The ordinary case, and the only one the athlete reaches by logging: the activity for
        // this session is already in hand, so nothing is enumerated and an update that would say
        // what is already on screen is dropped. Ten sets on one movement cost one update.
        if let showing, showing.sessionId == projection.attributes.sessionId {
            guard shown != projection.state else { return }
            await showing.update(to: projection.state)
            shown = projection.state
            return
        }

        // No handle, or one for a session that is over: this is the launch-time path, and the only
        // one that has to ask the system what it is actually holding.
        await endEverything(except: projection.attributes.sessionId)

        // An activity from a previous launch for this very session is adopted rather than
        // replaced — ending and re-requesting would flicker and restart the elapsed clock from
        // zero — and is then updated, because its content is as old as that launch.
        if let survivor = LiveActivityHandle.showing(session: projection.attributes.sessionId) {
            showing = survivor
            await survivor.update(to: projection.state)
        } else {
            showing = LiveActivityHandle.request(
                attributes: projection.attributes, state: projection.state)
        }
        shown = showing == nil ? nil : projection.state
    }

    /// Ends every activity this app owns, sparing the one session still in flight.
    ///
    /// It enumerates the system's list rather than only ending the handle it holds, because the
    /// handle is per-launch and the activities are not: a workout completed in a previous run, or
    /// one whose app was killed, leaves an activity this launch has never seen. Reading the real
    /// list is the only thing that reconciles that.
    private func endEverything(except sessionId: UUID? = nil) async {
        for activity in LiveActivityHandle.all where activity.sessionId != sessionId {
            await activity.end()
        }
        showing = nil
        shown = nil
    }
}

/// One `ActivityKit.Activity`, wrapped so this project's concurrency settings can reach it.
///
/// `Activity` is deliberately **not** `Sendable` — ActivityKit itself is built with strict
/// concurrency — while its `update` and `end` are nonisolated `async`. This project has
/// `NonisolatedNonsendingByDefault` off, so such a function leaves the caller's actor, and
/// `await activity.update(…)` is therefore a "sending risks causing data races" error from *every*
/// isolated context. No spelling avoids that without changing the project's concurrency settings,
/// which is the owner's decision and not this feature's to make. Containing the escape in one
/// 30-line type is the alternative.
///
/// **What makes sending safe is serialization at the only caller.** `WorkoutLiveActivity` is
/// `@MainActor` and holds the only handle, and `ActiveWorkoutStore.mirror()` chains each `present`
/// on the one before it, so at most one update or end for a given activity is ever in flight. The
/// wrapper also holds no mutable state of its own: the activity lives in the system's process and
/// this is a reference to it.
private struct LiveActivityHandle: @unchecked Sendable {
    private let activity: Activity<WorkoutActivityAttributes>

    var sessionId: UUID { activity.attributes.sessionId }

    /// Every activity of this app's, including ones started by a previous launch.
    static var all: [LiveActivityHandle] {
        Activity<WorkoutActivityAttributes>.activities.map(LiveActivityHandle.init)
    }

    static func showing(session: UUID) -> LiveActivityHandle? {
        all.first { $0.sessionId == session }
    }

    /// Asks the system to start an activity, or answers `nil` when it will not.
    ///
    /// The throw is routine rather than exceptional — Live Activities switched off in Settings, or
    /// too many already running — so it becomes an absence. Nothing is said about it because there
    /// is nothing the athlete would do, and the workout is unaffected either way.
    static func request(
        attributes: WorkoutActivityAttributes,
        state: WorkoutActivityAttributes.ContentState
    ) -> LiveActivityHandle? {
        guard ActivityAuthorizationInfo().areActivitiesEnabled else { return nil }
        guard let activity = try? Activity.request(
            attributes: attributes,
            content: ActivityContent(state: state, staleDate: nil))
        else { return nil }
        return LiveActivityHandle(activity: activity)
    }

    private init(activity: Activity<WorkoutActivityAttributes>) {
        self.activity = activity
    }

    /// No stale date: both clocks on this activity are rendered by the system from a date range, so
    /// the content stays correct however long it has been since the app last spoke. A stale date
    /// would dim a correct countdown.
    func update(to state: WorkoutActivityAttributes.ContentState) async {
        await activity.update(ActivityContent(state: state, staleDate: nil))
    }

    /// `.immediate` rather than the default, which leaves the activity on the Lock Screen for up to
    /// four hours as a record of something that finished. For a signed-out athlete that is a leak of
    /// the last session; for a discarded workout it is a countdown for a rest that will never end.
    func end() async {
        await activity.end(nil, dismissalPolicy: .immediate)
    }
}
