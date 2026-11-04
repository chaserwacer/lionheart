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
