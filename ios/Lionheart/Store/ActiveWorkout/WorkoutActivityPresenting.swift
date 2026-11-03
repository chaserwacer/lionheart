import Foundation

/// Keeping the Lock Screen's copy of the workout in line with the device's.
///
/// A seam at a **process** boundary, which earns a stand-in the same way `WorkoutSessionWriting`
/// does at the network one: the thing being stood in for is another process — ActivityKit, and the
/// widget extension behind it — and a unit test cannot run one. The lifecycle rules this protocol
/// exists to let us test are the whole risk of the feature, so they are tested against a fake that
/// records what it was asked to show.
///
/// It is one method on purpose. Every way a workout can change — started, resumed, restored from
/// disk, a set logged, a rest armed or skipped, a movement changed, a push refused, completed,
/// discarded, signed out — arrives here as the same question, and the answer is always "make the
/// Lock Screen agree with this". There is no `start`, no `stop` and no `update` to call in the
/// wrong order, and therefore no sequence of events that leaves an activity behind.
@MainActor
protocol WorkoutActivityPresenting {
    /// Brings the Lock Screen and Dynamic Island into line with `projection`.
    ///
    /// Starts an activity where none is showing, updates the one that is, and ends it when
    /// `projection` is `nil` — which is also what reconciles a stale activity left by a previous
    /// launch, because an activity whose session is not this projection's session is ended rather
    /// than adopted.
    ///
    /// - Parameter projection: what the Lock Screen should say, or `nil` to show nothing.
    func present(_ projection: WorkoutActivityProjection?) async
}

/// A Lock Screen that shows nothing, for a store running without one.
///
/// It is a value rather than an optional presenter because "nothing mirrors this workout" is a real
/// arrangement and not a missing collaborator: a unit test wants a store whose Lock Screen does
/// nothing, and `ActiveWorkoutStore` should have exactly one `present` call site rather than an
/// optional it has to remember to chain through.
struct NoWorkoutActivity: WorkoutActivityPresenting {
    func present(_ projection: WorkoutActivityProjection?) async {}
}
