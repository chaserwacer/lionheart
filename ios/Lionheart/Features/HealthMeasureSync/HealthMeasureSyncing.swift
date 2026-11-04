import Foundation

/// What the measurement sync needs from Health. A seam for the reason `HealthWorkoutSource` is one:
/// HealthKit cannot be filled from a unit test, and the sync's behaviour is all about what it is given.
protocol HealthMeasureSource: Sendable {
    var isAvailable: Bool { get }
    func needsAuthorization() async -> Bool
    func requestAuthorization() async throws
    func earliestReading() async throws -> Date?
    /// Every reading of every figure that started in `[from, to)`.
    func readings(from: Date, to: Date) async throws -> [HealthReading]
    func weighIns(from: Date, to: Date) async throws -> [BodyMassReading]
    func dateOfBirth() async -> CalendarDay?
}

extension HealthLibrary: HealthMeasureSource {
    func earliestReading() async throws -> Date? {
        try await MeasurementReader(store: store).earliestReading()
    }

    func readings(from: Date, to: Date) async throws -> [HealthReading] {
        try await MeasurementReader(store: store).readings(from: from, to: to)
    }

    func weighIns(from: Date, to: Date) async throws -> [BodyMassReading] {
        try await MeasurementReader(store: store).weighIns(from: from, to: to)
    }

    func dateOfBirth() async -> CalendarDay? {
        MeasurementReader(store: store).dateOfBirth()
    }
}

/// What the sync needs from the server.
protocol HealthMeasureImporting: Sendable {
    func offer(_ days: [MeasuredDayOffer]) async throws -> HealthMeasuresImportReport
    func offer(_ weighIns: [WeighIn]) async throws -> HealthBodyweightImportReport
    func apply(dateOfBirth: CalendarDay) async throws -> ProfileAge
}

extension HealthMeasuresAPI: HealthMeasureImporting {}

/// Where the sync keeps its mark between launches.
protocol HealthMeasureMarks: Sendable {
    func read() -> HealthMeasureMark
    func write(_ mark: HealthMeasureMark)
    func clear()
}

/// The mark as one small file in the app's own directory, gone at sign-out with the account.
struct HealthMeasureMarkFile: HealthMeasureMarks {
    private let file = DeviceFile(fileName: "health-measures.json")

    func read() -> HealthMeasureMark {
        file.read().flatMap { try? JSONDecoder().decode(HealthMeasureMark.self, from: $0) } ?? .start
    }

    func write(_ mark: HealthMeasureMark) {
        guard let data = try? JSONEncoder().encode(mark) else { return }
        try? file.write(data)
    }

    func clear() {
        file.clear()
    }
}
