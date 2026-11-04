import HealthKit

/// The app's one door into Apple Health: the store, whether this device has one, and access to
/// everything Lionheart reads.
///
/// **Read only.** Lionheart asks to read and never to write; the one writer, the sample seeding in
/// `SampleRuns`, exists only in a Debug build for the simulator.
///
/// Each kind of Health data Lionheart takes has a reader of its own beside this file — workouts are
/// `WorkoutReader` — and access is asked for all of their types at once, so the athlete sees one
/// sheet that lists everything Lionheart reads, and a reader added later joins it by adding its
/// types to `readTypes`.
final class HealthLibrary: Sendable {
    let store: HKHealthStore

    init(store: HKHealthStore = HKHealthStore()) {
        self.store = store
    }

    /// Every type a reader reads.
    static let readTypes: Set<HKObjectType> = WorkoutReader.readTypes.union(MeasurementReader.readTypes)

    /// Whether this device has a Health store at all. An iPad without the Health app does not.
    var isAvailable: Bool { HKHealthStore.isHealthDataAvailable() }

    /// Whether the athlete has yet to be asked. Health never says whether reading was allowed — a
    /// refusal reads as no data — so this is the one question it answers: has the sheet been shown.
    func needsAuthorization() async -> Bool {
        let status = try? await store.statusForAuthorizationRequest(toShare: [], read: Self.readTypes)
        switch status {
        case .shouldRequest, .unknown, nil: return true
        case .unnecessary: return false
        @unknown default: return false
        }
    }

    /// Shows Health's own sheet for everything Lionheart reads; answers once the athlete has chosen.
    func requestAuthorization() async throws {
        try await store.requestAuthorization(toShare: [], read: Self.readTypes)
    }
}
