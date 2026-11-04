import Foundation

/// The routes for what Apple's devices measured: days of figures offered and read back, weigh-ins,
/// and the date of birth.
///
/// Every offer is idempotent — a day offered again is restated, a weigh-in on a measured day is
/// kept as it was, a date of birth that matches the age changes nothing — so a sync can resend
/// whatever it is unsure of.
struct HealthMeasuresAPI: Sendable {
    let client: APIClient

    func offer(_ days: [MeasuredDayOffer]) async throws -> HealthMeasuresImportReport {
        try await client.post("/api/health/measurements", body: MeasuresBody(days: days))
    }

    func offer(_ weighIns: [WeighIn]) async throws -> HealthBodyweightImportReport {
        try await client.post("/api/health/bodyweights", body: WeighInsBody(weighIns: weighIns))
    }

    func apply(dateOfBirth: CalendarDay) async throws -> ProfileAge {
        try await client.put("/api/health/birth-date", body: BirthDateBody(dateOfBirth: dateOfBirth.wire))
    }

    /// Every figure kept from `from` through `to`, one series per figure and device.
    func history(
        from: CalendarDay, to: CalendarDay, remembering key: ReadKey
    ) async throws -> Remembered<HealthMeasureHistory> {
        try await client.get(
            "/api/health/measurements",
            query: [
                URLQueryItem(name: "From", value: from.wire),
                URLQueryItem(name: "To", value: to.wire),
            ],
            as: LenientList<HealthMeasureSeries>.self,
            remembering: key
        ).map { HealthMeasureHistory(series: $0.elements) }
    }
}

extension ReadKey {
    static let healthMeasures = ReadKey("health.measures")
}

private struct MeasuresBody: Encodable, Sendable {
    let days: [MeasuredDayOffer]
}

private struct WeighInsBody: Encodable, Sendable {
    let weighIns: [WeighIn]
}

private struct BirthDateBody: Encodable, Sendable {
    let dateOfBirth: String
}
