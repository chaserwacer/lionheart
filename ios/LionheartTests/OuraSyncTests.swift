import Foundation
import Testing

@testable import Lionheart

/// The Oura sync: what it asks for, when it asks on its own, and what it says when the server
/// refuses — against the server's real answers, stood in for at the network seam.
@Suite("Oura sync")
struct OuraSyncTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 3)

    // MARK: - The plan

    @Test("A sync runs from the newest stored day, included, through today")
    func syncIncludesTheNewestStoredDay() {
        let latest = CalendarDay(year: 2026, month: 8, day: 1)

        #expect(OuraSyncPlan.range(for: .filed(latest), today: Self.today)
            == DayRange(from: latest, to: Self.today))
    }

    @Test("A store that is current still syncs today, since today is still accruing")
    func aCurrentStoreSyncsToday() {
        #expect(OuraSyncPlan.range(for: .filed(Self.today), today: Self.today)
            == DayRange(from: Self.today, to: Self.today))
        #expect(!OuraSyncPlan.isBehind(.filed(Self.today), today: Self.today))
    }

    @Test("Today's activity without the night's readiness is still behind")
    func activityAloneIsBehind() {
        // Oura files a day's activity as soon as the ring syncs and its readiness only once the
        // night is scored; stopping at the first would leave the morning without its verdict.
        #expect(OuraSyncPlan.isBehind(.through(Self.today, readinessFiled: false), today: Self.today))
    }

    @Test("An empty store syncs the last thirty days")
    func anEmptyStoreSyncsAMonth() {
        let range = OuraSyncPlan.range(for: .nothingStored, today: Self.today)

        #expect(range?.to == Self.today)
        #expect(range.map { $0.from.days(until: $0.to) } == OuraSyncPlan.firstSyncDays - 1)
    }

    @Test("A stored day after the device's today plans nothing rather than a backwards range")
    func aDayAheadOfTheClockPlansNothing() {
        #expect(OuraSyncPlan.range(for: .filed(Self.today.adding(days: 1)), today: Self.today) == nil)
    }

    @Test("Coming back to the app syncs only when behind, idle, and not recently tried")
    func automaticSyncConditions() {
        let now = Self.today.middayDate()
        let behind = OuraSyncState.idle(.filed(Self.today.adding(days: -2)))

        #expect(OuraSyncPlan.warrantsAutomaticSync(behind, lastAttempt: nil, now: now, today: Self.today))
        #expect(!OuraSyncPlan.warrantsAutomaticSync(
            .idle(.filed(Self.today)), lastAttempt: nil, now: now, today: Self.today))
        #expect(!OuraSyncPlan.warrantsAutomaticSync(
            behind, lastAttempt: now.addingTimeInterval(-60), now: now, today: Self.today))
        #expect(OuraSyncPlan.warrantsAutomaticSync(
            behind, lastAttempt: now.addingTimeInterval(-OuraSyncPlan.automaticRetryInterval), now: now,
            today: Self.today))
        #expect(!OuraSyncPlan.warrantsAutomaticSync(
            .syncing(DayRange(from: Self.today, to: Self.today), from: .nothingStored),
            lastAttempt: nil, now: now, today: Self.today))
    }

    // MARK: - The server's answers

    @Test("A sync report decodes the server's own totals and each collection's tally")
    func reportDecodes() throws {
        let report = try Wire.decoder().decode(OuraSyncReport.self, from: Data("""
            {"range":{"from":"2026-08-01","to":"2026-11-03"},
             "collections":[{"collection":"daily_activity","received":3,"stored":2,
                             "rejected":["daily_activity document 'x' is missing its day."]},
                            {"collection":"daily_sleep","received":4,"stored":4,"rejected":[]}],
             "stored":6,"rejected":1}
            """.utf8))

        #expect(report.range == DayRange(
            from: CalendarDay(year: 2026, month: 8, day: 1), to: CalendarDay(year: 2026, month: 11, day: 3)))
        #expect(report.stored == 6)
        #expect(report.rejected == 1)
        #expect(report.collections.map(\.name) == ["daily_activity", "daily_sleep"])
    }

    @Test("A rejected Oura token arrives as a 502 and is reported as Oura's failure, in the server's words")
    func aRejectedTokenIsOurasFailure() throws {
        let detail = "The Oura API rejected the access token. (collection 'daily_activity')."

        #expect(try OuraSyncFailure.from(APIError.upstream(detail: detail)) == .ouraFailed(detail))
    }

    @Test("No stored credential arrives as a 409 and is a refusal")
    func noCredentialIsARefusal() throws {
        let detail = "This account has no Oura credential stored. Add one before syncing."

        #expect(try OuraSyncFailure.from(APIError.refused(detail: detail)) == .refused(detail))
    }

    @Test("Cancellation is not a sync failure")
    func cancellationIsRethrown() {
        #expect(throws: CancellationError.self) { try OuraSyncFailure.from(CancellationError()) }
    }

    // MARK: - The store

    @Test("A sync reads the coverage, syncs from it, and reports the coverage it left behind")
    @MainActor
    func aSyncRunsTheRailway() async {
        let before = CalendarDay(year: 2026, month: 8, day: 1)
        let server = AnOuraServer(coverages: [.filed(before), .filed(Self.today)], sync: .success(Self.report))
        let store = OuraSyncStore(oura: server, now: { Self.today.middayDate() })
        var reread = 0
        store.onSynced { reread += 1 }

        await store.sync()

        #expect(await server.asked == [DayRange(from: before, to: Self.today)])
        #expect(store.state == .synced(Self.report, coverage: .filed(Self.today)))
        #expect(reread == 1)
    }

    @Test("A failed sync keeps the coverage it knew and states Oura's failure")
    @MainActor
    func aFailedSyncKeepsTheCoverage() async {
        let before = CalendarDay(year: 2026, month: 8, day: 1)
        let server = AnOuraServer(
            coverages: [.filed(before)], sync: .failure(APIError.upstream(detail: "Oura said no.")))
        let store = OuraSyncStore(oura: server, now: { Self.today.middayDate() })

        await store.sync()

        #expect(store.state == .failed(.ouraFailed("Oura said no."), coverage: .filed(before)))
    }

    @Test("Coming back to the app onto current data reads the coverage and does not sync")
    @MainActor
    func currentDataIsNotSyncedAutomatically() async {
        let server = AnOuraServer(coverages: [.filed(Self.today)], sync: .success(Self.report))
        let store = OuraSyncStore(oura: server, now: { Self.today.middayDate() })

        await store.syncIfBehind()

        #expect(await server.asked.isEmpty)
        #expect(store.state == .idle(.filed(Self.today)))
    }

    @Test("After a failed automatic sync, the next return to the app inside the interval does not ask again")
    @MainActor
    func aFailureWaitsOutTheInterval() async {
        let server = AnOuraServer(
            coverages: [.filed(Self.today.adding(days: -3))],
            sync: .failure(APIError.upstream(detail: "Oura said no.")))
        let store = OuraSyncStore(oura: server, now: { Self.today.middayDate() })

        await store.syncIfBehind()
        await store.syncIfBehind()

        #expect(await server.asked.count == 1)
    }

    @Test("Signing out during a sync leaves nothing of the last athlete's coverage behind")
    @MainActor
    func signingOutMidSyncWritesNothing() async throws {
        let server = AnOuraServer(
            coverages: [.filed(Self.today.adding(days: -3))], sync: .success(Self.report),
            syncTakes: .seconds(5))
        let store = OuraSyncStore(oura: server, now: { Self.today.middayDate() })

        let syncing = Task { await store.sync() }
        while !store.state.isSyncing { await Task.yield() }
        store.reset()
        await syncing.value

        #expect(store.state == .unknown)
    }

    private static let report = OuraSyncReport(range: nil, stored: 12, rejected: 0, collections: [])
}

/// The server's side of the sync: answers coverage reads in order, repeating the last, and records
/// every range it was asked to sync.
private actor AnOuraServer: OuraSyncing {
    private var coverages: [OuraCoverage]
    private let outcome: Result<OuraSyncReport, APIError>
    private let syncTakes: Duration
    private(set) var asked: [DayRange] = []

    init(coverages: [OuraCoverage], sync: Result<OuraSyncReport, APIError>, syncTakes: Duration = .zero) {
        self.coverages = coverages
        self.outcome = sync
        self.syncTakes = syncTakes
    }

    func coverage() async throws -> OuraCoverage {
        coverages.count > 1 ? coverages.removeFirst() : coverages[0]
    }

    func sync(_ range: DayRange) async throws -> OuraSyncReport {
        asked.append(range)
        if syncTakes > .zero { try await Task.sleep(for: syncTakes) }
        return try outcome.get()
    }
}

private extension OuraCoverage {
    /// Coverage through a day whose readiness Oura has filed — the settled case most tests mean.
    static func filed(_ day: CalendarDay) -> OuraCoverage { .through(day, readinessFiled: true) }
}
