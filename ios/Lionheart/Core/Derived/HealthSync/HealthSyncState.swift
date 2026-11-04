import Foundation

/// Where the Apple Health sync stands, as its row shows it.
///
/// When the last sync finished travels with every case once it is known, because the row always
/// says how current the workouts are — "last synced 14:02" stays true while a sync is out and after one
/// fails.
enum HealthSyncState: Equatable, Sendable {
    /// What is kept on the device has not been read yet.
    case unknown
    /// This device has no Health store — an iPad without the Health app, for instance.
    case unavailable
    /// The athlete has not yet been asked to let Lionheart read Health.
    case notConnected
    case idle(lastSynced: Date?)
    /// A sync is out; `progress` is what it has done so far.
    case syncing(progress: HealthSyncTally, lastSynced: Date?)
    case synced(HealthSyncTally, at: Date)
    /// The last sync stopped. What it finished before stopping is kept: the workouts it offered are
    /// on the server, and the next sync resumes after them.
    case failed(HealthSyncFailure, done: HealthSyncTally, lastSynced: Date?)

    var lastSynced: Date? {
        switch self {
        case .unknown, .unavailable, .notConnected: nil
        case .idle(let lastSynced), .syncing(_, let lastSynced), .failed(_, _, let lastSynced): lastSynced
        case .synced(_, let at): at
        }
    }

    /// What the last sync to finish, or stop, did.
    var done: HealthSyncTally? {
        switch self {
        case .unknown, .unavailable, .notConnected, .idle, .syncing: nil
        case .synced(let tally, _), .failed(_, let tally, _): tally
        }
    }

    var isSyncing: Bool {
        switch self {
        case .syncing: true
        case .unknown, .unavailable, .notConnected, .idle, .synced, .failed: false
        }
    }

    /// Whether a sync can run without asking the athlete anything first.
    var isConnected: Bool {
        switch self {
        case .idle, .syncing, .synced, .failed: true
        case .unknown, .unavailable, .notConnected: false
        }
    }
}

/// What one sync did with the workouts it offered.
struct HealthSyncTally: Equatable, Sendable {
    /// Became sessions.
    let imported: Int
    /// The server had them already.
    let alreadyImported: Int
    /// Deliberately left out, by reason.
    let leftOut: [HealthWorkoutExclusion: Int]
    /// The server refused them, in its own words.
    let refused: [String]

    static let empty = HealthSyncTally(imported: 0, alreadyImported: 0, leftOut: [:], refused: [])

    var offered: Int { imported + alreadyImported + leftOut.values.reduce(0, +) + refused.count }

    func adding(_ reply: HealthOfferReply) -> HealthSyncTally {
        switch reply {
        case .answered(let outcome): adding(outcome)
        case .refused(let reason): refusing(reason)
        }
    }

    func adding(_ outcome: HealthImportOutcome) -> HealthSyncTally {
        switch outcome {
        case .imported:
            HealthSyncTally(imported: imported + 1, alreadyImported: alreadyImported, leftOut: leftOut, refused: refused)
        case .alreadyImported:
            HealthSyncTally(imported: imported, alreadyImported: alreadyImported + 1, leftOut: leftOut, refused: refused)
        case .notImported(let reason):
            HealthSyncTally(
                imported: imported, alreadyImported: alreadyImported,
                leftOut: leftOut.merging([reason: 1], uniquingKeysWith: +), refused: refused)
        case .unrecognised(let type):
            refusing("An answer this version cannot read (\(type)).")
        }
    }

    func refusing(_ reason: String) -> HealthSyncTally {
        HealthSyncTally(imported: imported, alreadyImported: alreadyImported, leftOut: leftOut, refused: refused + [reason])
    }
}
