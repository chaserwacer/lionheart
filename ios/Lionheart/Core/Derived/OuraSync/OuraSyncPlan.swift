import Foundation

/// What to sync, and whether to sync without being asked.
enum OuraSyncPlan {
    /// How far back a first sync reaches when nothing is stored. The archive importer is how
    /// years arrive; a sync from an empty store only has to start the record.
    static let firstSyncDays = 30

    /// How long an automatic sync waits after the last attempt before trying again.
    ///
    /// Without it every return to the app would ask Oura again while the ring has not synced to
    /// Oura's own app — the store stays behind, so the next foreground would try again, and so on.
    static let automaticRetryInterval: TimeInterval = 30 * 60

    /// The days to ask Oura for: from the newest stored day through today.
    ///
    /// The newest stored day is **included**, not skipped. It is usually provisional — today's
    /// activity keeps accruing after the morning's readiness is filed, and Oura revises a day after
    /// the ring next syncs — and the server's upsert makes re-pulling it free of duplicates. A
    /// store that is already current therefore still syncs today, which is what a tap on Sync
    /// means. `nil` only when the newest stored day is after today, which a device clock behind
    /// the ring's can produce, and asking for a backwards range would be refused anyway.
    static func range(for coverage: OuraCoverage, today: CalendarDay) -> DayRange? {
        switch coverage {
        case .nothingStored:
            return DayRange(from: today.adding(days: 1 - firstSyncDays), to: today)
        case .through(let latest):
            return latest > today ? nil : DayRange(from: latest, to: today)
        }
    }

    /// Whether the stored data stops before today.
    static func isBehind(_ coverage: OuraCoverage, today: CalendarDay) -> Bool {
        switch coverage {
        case .nothingStored: true
        case .through(let latest): latest < today
        }
    }

    /// Whether coming back to the app should start a sync on its own: the data is behind, nothing
    /// is out already, and the last attempt — succeeded or not — was long enough ago.
    ///
    /// An unknown coverage warrants one: the read of it is the sync's first step, so the attempt
    /// finds out. A failed attempt waits out the interval like any other, so a rejected token is
    /// not asked about on every foreground.
    static func warrantsAutomaticSync(
        _ state: OuraSyncState, lastAttempt: Date?, now: Date, today: CalendarDay
    ) -> Bool {
        guard !state.isSyncing else { return false }
        if let lastAttempt, now.timeIntervalSince(lastAttempt) < automaticRetryInterval { return false }
        guard let coverage = state.coverage else { return true }
        return isBehind(coverage, today: today)
    }
}
