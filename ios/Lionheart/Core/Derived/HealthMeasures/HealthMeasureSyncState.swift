import Foundation

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
