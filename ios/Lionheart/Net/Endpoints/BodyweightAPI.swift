import Foundation

/// The bodyweight routes: one measurement per calendar day.
///
/// Logging is a `PUT` to the day, so sending the same measurement twice — a retry after an answer
/// that never arrived — writes the same row, and logging again on a day corrects it.
struct BodyweightAPI: Sendable {
    let client: APIClient

    private static let root = "/api/bodyweight"

    /// Every measurement in the range, oldest first.
    func measurements(
        from: CalendarDay, to: CalendarDay, remembering key: ReadKey
    ) async throws -> Remembered<[BodyweightMeasurement]> {
        try await client.get(
            Self.root,
            query: [
                URLQueryItem(name: "From", value: from.wire),
                URLQueryItem(name: "To", value: to.wire),
            ],
            as: LenientList<BodyweightMeasurement>.self,
            remembering: key
        ).map(\.elements)
    }

    func log(_ weight: Double, _ unit: WeightUnit, on day: CalendarDay) async throws -> BodyweightMeasurement {
        try await client.put("\(Self.root)/\(day.wire)", body: BodyweightBody(weight: weight, unit: unit.wire))
    }

    func delete(on day: CalendarDay) async throws {
        try await client.delete("\(Self.root)/\(day.wire)")
    }
}

private struct BodyweightBody: Encodable, Sendable {
    let weight: Double
    let unit: Int
}
