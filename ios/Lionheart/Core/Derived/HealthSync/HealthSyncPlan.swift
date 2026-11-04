import Foundation

/// When the Apple Health sync starts on its own.
enum HealthSyncPlan {
    /// How long a sync started on its own waits after the last attempt before trying again.
    ///
    /// A run reaches Health some minutes after it ends, once the recording app has uploaded it, so
    /// every return to the app need not ask; and a sync that failed is not retried on every return.
    static let automaticRetryInterval: TimeInterval = 15 * 60

    /// Whether coming back to the app should sync: Health is connected, nothing is out, and the
    /// last attempt — succeeded or not — was long enough ago.
    ///
    /// Never while Health is not connected: asking for access is the athlete's decision, made from
    /// the row, not something the app springs on him at launch.
    static func warrantsAutomaticSync(_ state: HealthSyncState, lastAttempt: Date?, now: Date) -> Bool {
        guard state.isConnected, !state.isSyncing else { return false }
        guard let lastAttempt else { return true }
        return now.timeIntervalSince(lastAttempt) >= automaticRetryInterval
    }
}

/// What the device keeps between syncs: where in Health's history the last one finished, and when.
///
/// The anchor is HealthKit's own, archived — Health hands back only what was added or changed after
/// it, so each sync reads the new workouts and not the whole history again. It is moved on only
/// once every workout before it has been answered by the server, so a sync that stops resumes where
/// the last answer was given rather than skipping what it never sent.
struct HealthSyncMark: Codable, Equatable, Sendable {
    let anchor: Data?
    let lastSynced: Date?

    static let start = HealthSyncMark(anchor: nil, lastSynced: nil)
}
