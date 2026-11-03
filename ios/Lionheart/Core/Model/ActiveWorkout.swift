import Foundation

/// The in-flight session as the device holds it.
///
/// The one piece of state Lionheart owns rather than reads, because the gym is where the signal is
/// worst and a set lost to a force-quit is the single failure that would stop the owner using the
/// app. It is a value, and `ActiveWorkoutStore` writes the whole of it to disk on every change —
/// which is only safe to do cheaply because it is small and immutable.
///
/// This foundation carries the identity of the session and when it started. The sets logged so far,
/// the current movement and the rest clock belong here too and are the training-feature agent's to
/// add; `version` exists so that when they do, a document written by this build is recognised as
/// an older shape rather than silently misread.
struct ActiveWorkout: Equatable, Sendable, Codable {
    /// The shape of the persisted document. Bumped when a stored field changes meaning.
    static let currentVersion = 1

    let version: Int
    let sessionId: UUID
    let startedAt: Date
    /// Whether everything logged locally has reached the server. The bar shows an unsynced state
    /// rather than treating a failed push as an error: the next push carries everything.
    let isSynced: Bool

    init(sessionId: UUID, startedAt: Date, isSynced: Bool = true, version: Int = Self.currentVersion) {
        self.version = version
        self.sessionId = sessionId
        self.startedAt = startedAt
        self.isSynced = isSynced
    }

    /// How long the session has been running at `now`. Never negative, so a device clock that moved
    /// backwards cannot show a workout counting down.
    func elapsed(at now: Date) -> TimeInterval {
        max(0, now.timeIntervalSince(startedAt))
    }

    func markingSynced(_ synced: Bool) -> ActiveWorkout {
        ActiveWorkout(sessionId: sessionId, startedAt: startedAt, isSynced: synced, version: version)
    }

    /// Whether a document read from disk is one this build understands.
    ///
    /// A newer document is refused rather than read: a downgraded build that half-understood a
    /// future shape would write it back truncated, which is how logged sets get lost for good.
    var isReadable: Bool { version <= Self.currentVersion }
}
