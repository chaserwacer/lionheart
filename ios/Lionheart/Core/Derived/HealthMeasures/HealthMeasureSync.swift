import Foundation

/// What the measurement sync keeps between launches: the last day it has offered everything up to,
/// and when it last finished.
///
/// A day rather than HealthKit's anchor, which suits workouts and not these. A day's figures change
/// after the day: the watch hands over a night's readings when it next reaches the phone, and
/// Health issues a VO2max or a steadiness estimate after the walks it is drawn from. So every sync
/// reads again from a week before this day, and the server restates the days that grew.
struct HealthMeasureMark: Codable, Equatable, Sendable {
    let syncedThrough: String?
    let lastSynced: Date?

    init(syncedThrough: CalendarDay?, lastSynced: Date?) {
        self.syncedThrough = syncedThrough?.wire
        self.lastSynced = lastSynced
    }

    var syncedThroughDay: CalendarDay? { syncedThrough.flatMap(CalendarDay.init(wire:)) }

    static let start = HealthMeasureMark(syncedThrough: nil, lastSynced: nil)
}

/// Which days a sync reads, and when it starts on its own.
enum HealthMeasureSyncPlan {
    /// Days per read: a quarter of walking metrics is a few thousand readings, small enough that a
    /// sync stopped partway has little to read again and large enough that six years is two dozen.
    static let windowDays = 90

    /// How far back from the last day synced each sync reads again (see `HealthMeasureMark`).
    static let rereadDays = 7

    /// The figures move by the day, so returning to the app need not ask more than hourly.
    static let automaticRetryInterval: TimeInterval = 60 * 60

    /// The windows of days to read, oldest first, through today: from a week before the last day
    /// synced, or on a first sync from the oldest reading Health holds.
    static func windows(earliest: CalendarDay?, mark: HealthMeasureMark, today: CalendarDay) -> [DayRange] {
        let start = min(mark.syncedThroughDay.map { $0.adding(days: -rereadDays) } ?? earliest ?? today, today)
        return Array(sequence(first: start) { $0.adding(days: windowDays) }.prefix { $0 <= today })
            .map { from in DayRange(from: from, to: min(from.adding(days: windowDays - 1), today)) }
    }

    static func warrantsAutomaticSync(_ state: HealthMeasureSyncState, lastAttempt: Date?, now: Date) -> Bool {
        guard state.isConnected, !state.isSyncing else { return false }
        guard let lastAttempt else { return true }
        return now.timeIntervalSince(lastAttempt) >= automaticRetryInterval
    }
}

/// What one sync did.
struct HealthMeasureTally: Equatable, Sendable {
    /// Days the server did not hold before.
    let daysRecorded: Int
    /// Days it held whose readings had grown or changed.
    let daysRestated: Int
    /// Days whose numbers the server refused as readings of their figure.
    let daysUnreadable: Int
    /// Readings another app wrote, left out on the phone.
    let readingsFromOtherApps: Int
    /// Days of bodyweight filled from Health's weigh-ins.
    let bodyweightsAdded: Int
    /// Weighted dips and pull-ups whose system load now reads a different bodyweight.
    let setsReweighed: Int

    static let empty = HealthMeasureTally(
        daysRecorded: 0, daysRestated: 0, daysUnreadable: 0, readingsFromOtherApps: 0, bodyweightsAdded: 0, setsReweighed: 0)

    /// Whether the server holds anything it did not before.
    var changedAnything: Bool { daysRecorded + daysRestated + bodyweightsAdded > 0 }

    func adding(_ report: HealthMeasuresImportReport) -> HealthMeasureTally {
        HealthMeasureTally(
            daysRecorded: daysRecorded + report.recorded,
            daysRestated: daysRestated + report.restated,
            daysUnreadable: daysUnreadable + report.unreadable,
            readingsFromOtherApps: readingsFromOtherApps,
            bodyweightsAdded: bodyweightsAdded,
            setsReweighed: setsReweighed)
    }

    func adding(_ report: HealthBodyweightImportReport) -> HealthMeasureTally {
        HealthMeasureTally(
            daysRecorded: daysRecorded,
            daysRestated: daysRestated,
            daysUnreadable: daysUnreadable,
            readingsFromOtherApps: readingsFromOtherApps,
            bodyweightsAdded: bodyweightsAdded + report.added,
            setsReweighed: setsReweighed + report.setsReweighed)
    }

    func leavingOut(readingsFromOtherApps count: Int) -> HealthMeasureTally {
        HealthMeasureTally(
            daysRecorded: daysRecorded,
            daysRestated: daysRestated,
            daysUnreadable: daysUnreadable,
            readingsFromOtherApps: readingsFromOtherApps + count,
            bodyweightsAdded: bodyweightsAdded,
            setsReweighed: setsReweighed)
    }
}

/// Where the measurement sync stands, as its row shows it.
enum HealthMeasureSyncState: Equatable, Sendable {
    case unknown
    /// This device has no Health store.
    case unavailable
    /// The athlete has not been asked for everything Lionheart reads.
    case notConnected
    case idle(lastSynced: Date?)
    /// Out, through `through` so far.
    case syncing(HealthMeasureTally, through: CalendarDay?, lastSynced: Date?)
    case synced(HealthMeasureTally, at: Date)
    /// Stopped. What it offered before stopping is on the server, and the next sync resumes there.
    case failed(String, done: HealthMeasureTally, lastSynced: Date?)

    var isSyncing: Bool {
        switch self {
        case .syncing: true
        case .unknown, .unavailable, .notConnected, .idle, .synced, .failed: false
        }
    }

    var isConnected: Bool {
        switch self {
        case .idle, .syncing, .synced, .failed: true
        case .unknown, .unavailable, .notConnected: false
        }
    }
}
