import Foundation
import Synchronization
import Testing

@testable import Lionheart

/// The Apple Health sync: what it counts, when it runs on its own, how it walks Health's pages, and
/// what it keeps when it stops partway.
@Suite("Apple Health sync")
struct HealthSyncTests {
    private static let now = Date(timeIntervalSince1970: 1_790_000_000)

    // MARK: - The tally

    @Test("Each answer is counted under its own kind, and left-out runs by their reason")
    func theTallyCountsEachAnswer() {
        let id = UUID()
        let tally = [
            HealthImportOutcome.imported(trainingSessionId: id),
            .imported(trainingSessionId: id),
            .alreadyImported(trainingSessionId: id),
            .notImported(.readDirectly),
            .notImported(.readDirectly),
            .notImported(.otherActivity),
        ].reduce(HealthSyncTally.empty) { $0.adding($1) }.refusing("Not a place on Earth.")

        #expect((tally.imported, tally.alreadyImported) == (2, 1))
        #expect(tally.leftOut == [.readDirectly: 2, .otherActivity: 1])
        #expect(tally.refused == ["Not a place on Earth."])
        #expect(tally.offered == 7)
    }

    // MARK: - When it runs on its own

    @Test("Coming back to the app syncs only when connected, idle, and not tried inside the interval")
    func automaticSyncsWaitTheirTurn() {
        let idle = HealthSyncState.idle(lastSynced: nil)
        let recently = Self.now.addingTimeInterval(-60)
        let longAgo = Self.now.addingTimeInterval(-HealthSyncPlan.automaticRetryInterval)

        #expect(HealthSyncPlan.warrantsAutomaticSync(idle, lastAttempt: nil, now: Self.now))
        #expect(HealthSyncPlan.warrantsAutomaticSync(idle, lastAttempt: longAgo, now: Self.now))
        #expect(!HealthSyncPlan.warrantsAutomaticSync(idle, lastAttempt: recently, now: Self.now))
        #expect(!HealthSyncPlan.warrantsAutomaticSync(.notConnected, lastAttempt: nil, now: Self.now))
        #expect(!HealthSyncPlan.warrantsAutomaticSync(.unavailable, lastAttempt: nil, now: Self.now))
        #expect(!HealthSyncPlan.warrantsAutomaticSync(
            .syncing(progress: .empty, lastSynced: nil), lastAttempt: nil, now: Self.now))
    }

    @Test("A workout the server refuses is that workout's answer; an unreachable server stops the sync")
    func whichFailuresStopTheSync() {
        #expect(HealthSyncFailure.refusalOfOneWorkout(APIError.refused(detail: "A record is withdrawn.")) == "A record is withdrawn.")
        #expect(HealthSyncFailure.refusalOfOneWorkout(APIError.invalid(fields: [:], detail: "Not a heart rate.")) == "Not a heart rate.")
        #expect(HealthSyncFailure.refusalOfOneWorkout(APIError.transport("offline")) == nil)
        #expect(HealthSyncFailure.refusalOfOneWorkout(APIError.server(detail: nil)) == nil)
    }

    // MARK: - The store

    @Test("A sync offers every page's runs, keeps the last anchor, and says what it did")
    @MainActor
    func aSyncWalksEveryPage() async {
        let health = AHealth(pages: [
            .init(workouts: [Self.run(), Self.run()], anchor: Data("one".utf8), isLast: false),
            .init(workouts: [Self.run()], anchor: Data("two".utf8), isLast: true),
        ])
        let server = AServer(answers: [.imported(trainingSessionId: UUID()), .alreadyImported(trainingSessionId: UUID()), .notImported(.otherActivity)])
        let marks = Marks()
        let store = HealthSyncStore(health: health, server: server, marks: marks, now: { Self.now })
        var reread = 0
        store.onImported { reread += 1 }

        await store.sync()

        #expect(health.askedAfter == [nil, Data("one".utf8)])
        #expect(server.offered.count == 3)
        #expect(marks.mark == HealthSyncMark(anchor: Data("two".utf8), lastSynced: Self.now, heldBack: []))
        guard case .synced(let tally, let at) = store.state else {
            Issue.record("Expected a finished sync, got \(store.state)")
            return
        }
        #expect((tally.imported, tally.alreadyImported, tally.leftOut) == (1, 1, [.otherActivity: 1]))
        #expect(at == Self.now)
        #expect(reread == 1)
    }

    @Test("A workout the server refuses is counted and the sync goes on to the next")
    @MainActor
    func aRefusedWorkoutDoesNotStopTheSync() async {
        let health = AHealth(pages: [.init(workouts: [Self.run(), Self.run()], anchor: Data("one".utf8), isLast: true)])
        let server = AServer(answers: [], failures: [APIError.refused(detail: "A record is withdrawn.")])
        let store = HealthSyncStore(health: health, server: server, marks: Marks(), now: { Self.now })

        await store.sync()

        #expect(server.offered.count == 2)
        #expect(store.state.done?.refused == ["A record is withdrawn."])
        #expect(store.state.done?.imported == 1)
    }

    @Test("A sync that loses the server partway keeps the anchor before that page and what it had done")
    @MainActor
    func aStoppedSyncResumesAtThePageItStoppedIn() async {
        let health = AHealth(pages: [
            .init(workouts: [Self.run()], anchor: Data("one".utf8), isLast: false),
            .init(workouts: [Self.run(), Self.run()], anchor: Data("two".utf8), isLast: true),
        ])
        let server = AServer(answers: [], failures: [nil, nil, APIError.transport("offline")])
        let marks = Marks()
        let store = HealthSyncStore(health: health, server: server, marks: marks, now: { Self.now })
        var reread = 0
        store.onImported { reread += 1 }

        await store.sync()

        #expect(marks.mark == HealthSyncMark(anchor: Data("one".utf8), lastSynced: nil))
        guard case .failed(let failure, let done, _) = store.state else {
            Issue.record("Expected a stopped sync, got \(store.state)")
            return
        }
        #expect(failure == .offline)
        #expect(done.imported == 2)
        #expect(reread == 1)
    }

    @Test("Pulling to refresh before Health is connected reads nothing and asks nothing")
    @MainActor
    func anUnconnectedHealthIsNotRead() async {
        let health = AHealth(pages: [], needsAuthorization: true)
        let store = HealthSyncStore(health: health, server: AServer(answers: []), marks: Marks(), now: { Self.now })

        await store.syncIfConnected()

        #expect(store.state == .notConnected)
        #expect(health.askedAfter.isEmpty)
    }

    @Test("A run the server turns down is held back, offered again first next time, and let go once answered")
    @MainActor
    func aRefusedRunIsOfferedAgain() async {
        let refused = Self.run()
        let health = AHealth(pages: [
            .init(workouts: [refused, Self.run()], anchor: Data("one".utf8), isLast: true),
        ], held: [refused])
        let server = AServer(answers: [], failures: [APIError.refused(detail: "A record is withdrawn.")])
        let marks = Marks()
        let store = HealthSyncStore(health: health, server: server, marks: marks, now: { Self.now })

        await store.sync()
        #expect(marks.mark.heldBack == [refused.workoutId])

        await store.sync()

        #expect(health.askedFor == [[refused.workoutId]])
        #expect(server.offered.last == refused.workoutId)
        #expect(marks.mark.heldBack.isEmpty)
    }

    @Test("Signing out forgets the anchor, so the next athlete's first sync offers everything")
    @MainActor
    func signingOutForgetsTheAnchor() async {
        let marks = Marks()
        marks.write(HealthSyncMark(anchor: Data("one".utf8), lastSynced: Self.now))
        let store = HealthSyncStore(health: AHealth(pages: []), server: AServer(answers: []), marks: marks, now: { Self.now })

        store.reset()

        #expect(marks.mark == .start)
        #expect(store.state == .unknown)
    }

    // MARK: - Reading a run in miles

    @Test("A pace per kilometre reads per mile by the length of a mile")
    func paceConvertsByTheMile() {
        #expect(RunUnit.kilometres.pace(perKilometre: 300) == "5:00 /km")
        #expect(RunUnit.miles.pace(perKilometre: 300) == "8:03 /mi")
        #expect(RunUnit.miles.distance(16_093.44) == "10.00 mi")
    }

    // MARK: - Fixtures

    private static func run() -> HealthWorkoutImport {
        HealthWorkoutImport(
            workoutId: UUID(), activity: "running", indoor: false, appName: "Strava", appBundleId: "com.strava.stravaride",
            start: now, end: now.addingTimeInterval(1800), timeZone: .gmt, duration: 1800,
            distanceMeters: 5000, elevationAscendedMeters: nil, route: [], heartRate: [], metrics: [])
    }
}

/// Health as the sync sees it: pages handed out in order, every anchor it was asked after, and the
/// workouts it holds by id.
private final class AHealth: HealthWorkoutSource {
    private let state: Mutex<(pages: [HealthWorkoutPage], asked: [Data?], askedFor: [[UUID]])>
    private let needsAsking: Bool
    private let held: [HealthWorkoutImport]

    init(pages: [HealthWorkoutPage], needsAuthorization: Bool = false, held: [HealthWorkoutImport] = []) {
        state = Mutex((pages, [], []))
        needsAsking = needsAuthorization
        self.held = held
    }

    var isAvailable: Bool { true }
    var askedAfter: [Data?] { state.withLock { $0.asked } }
    var askedFor: [[UUID]] { state.withLock { $0.askedFor } }

    func workouts(_ ids: [UUID]) async throws -> [HealthWorkoutImport] {
        state.withLock { $0.askedFor.append(ids) }
        return held.filter { ids.contains($0.workoutId) }
    }

    func needsAuthorization() async -> Bool { needsAsking }
    func requestAuthorization() async throws {}

    func page(after anchor: Data?) async throws -> HealthWorkoutPage {
        state.withLock { held in
            held.asked.append(anchor)
            return held.pages.isEmpty
                ? HealthWorkoutPage(workouts: [], anchor: anchor, isLast: true)
                : held.pages.removeFirst()
        }
    }
}

/// The server as the sync sees it: each offer answered in turn, or failed where a failure is given
/// for that turn; once the answers run out, every offer is imported.
private final class AServer: HealthImporting {
    private let state: Mutex<(answers: [HealthImportOutcome], failures: [APIError?], offered: [UUID])>

    init(answers: [HealthImportOutcome], failures: [APIError?] = []) {
        state = Mutex((answers, failures, []))
    }

    var offered: [UUID] { state.withLock { $0.offered } }

    /// Restates a refusal of one workout as `HealthAPI` does, so the store meets the reply it will.
    func offer(_ workout: HealthWorkoutImport) async throws -> HealthOfferReply {
        try state.withLock { held in
            held.offered.append(workout.workoutId)
            if !held.failures.isEmpty, let failure = held.failures.removeFirst() {
                guard let reason = HealthSyncFailure.refusalOfOneWorkout(failure) else { throw failure }
                return .refused(reason)
            }
            return .answered(held.answers.isEmpty ? .imported(trainingSessionId: workout.workoutId) : held.answers.removeFirst())
        }
    }
}

private final class Marks: HealthSyncMarks {
    private let held = Mutex(HealthSyncMark.start)

    var mark: HealthSyncMark { held.withLock { $0 } }

    func read() -> HealthSyncMark { mark }
    func write(_ mark: HealthSyncMark) { held.withLock { $0 = mark } }
    func clear() { held.withLock { $0 = .start } }
}
