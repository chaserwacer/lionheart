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

/// What the device keeps between syncs: where in Health's history the last one finished, when, and
/// the workouts the server turned down that are worth offering again.
///
/// The anchor is HealthKit's own, archived — Health hands back only what was added or changed after
/// it, so each sync reads the new workouts and not the whole history again. It is moved on only
/// once every workout before it has been answered by the server, so a sync that stops resumes where
/// the last answer was given rather than skipping what it never sent.
///
/// A workout the server refused has been answered, so the anchor passes it; its Health id is held
/// back instead, and every sync offers it again first. The server's reasons for refusing one are
/// conditions that can change — a withdrawn record that blocks placing an older run — and a run
/// passed by the anchor and forgotten would never reach the log.
struct HealthSyncMark: Codable, Equatable, Sendable {
    let anchor: Data?
    let lastSynced: Date?
    let heldBack: [UUID]

    init(anchor: Data?, lastSynced: Date?, heldBack: [UUID] = []) {
        self.anchor = anchor
        self.lastSynced = lastSynced
        self.heldBack = heldBack
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        self.init(
            anchor: try container.decodeIfPresent(Data.self, forKey: .anchor),
            lastSynced: try container.decodeIfPresent(Date.self, forKey: .lastSynced),
            heldBack: try container.decodeIfPresent([UUID].self, forKey: .heldBack) ?? [])
    }

    static let start = HealthSyncMark(anchor: nil, lastSynced: nil)
}
