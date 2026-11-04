import Foundation

/// What the Apple Health sync needs from Health: whether this device has it, whether the athlete
/// has been asked, and the runs added after an anchor, a page at a time.
///
/// A seam for the reason `OuraSyncing` is one: the thing stood in for is outside the app — here
/// HealthKit, which a unit test cannot fill — and the sync's interesting behaviour is all about what
/// it is given.
protocol HealthWorkoutSource: Sendable {
    var isAvailable: Bool { get }
    func needsAuthorization() async -> Bool
    func requestAuthorization() async throws
    func page(after anchor: Data?) async throws -> HealthWorkoutPage
    /// The workouts Health still holds with these ids, oldest first; one deleted since is not there.
    func workouts(_ ids: [UUID]) async throws -> [HealthWorkoutImport]
}

extension HealthLibrary: HealthWorkoutSource {
    func page(after anchor: Data?) async throws -> HealthWorkoutPage {
        try await WorkoutReader(store: store).page(after: anchor)
    }

    func workouts(_ ids: [UUID]) async throws -> [HealthWorkoutImport] {
        try await WorkoutReader(store: store).workouts(ids)
    }
}

/// What the sync needs from the server: a reply for each workout offered.
protocol HealthImporting: Sendable {
    func offer(_ workout: HealthWorkoutImport) async throws -> HealthOfferReply
}

extension HealthAPI: HealthImporting {}

/// Where the sync keeps its mark between launches.
protocol HealthSyncMarks: Sendable {
    func read() -> HealthSyncMark
    func write(_ mark: HealthSyncMark)
    func clear()
}

/// The mark as one small file in the app's own directory, gone at sign-out with the account it was
/// synced for.
struct HealthSyncMarkFile: HealthSyncMarks {
    private let file = DeviceFile(fileName: "health-sync.json")

    func read() -> HealthSyncMark {
        file.read().flatMap { try? JSONDecoder().decode(HealthSyncMark.self, from: $0) } ?? .start
    }

    func write(_ mark: HealthSyncMark) {
        guard let data = try? JSONEncoder().encode(mark) else { return }
        try? file.write(data)
    }

    func clear() {
        file.clear()
    }
}
