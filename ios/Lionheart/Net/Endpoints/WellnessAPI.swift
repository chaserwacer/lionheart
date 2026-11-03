import Foundation

/// The wellness routes, read off `Endpoints/WellnessEndpoints/` — they predate the remodel and are
/// not in `docs/api-surface.md`.
///
/// The range read is a `GET` whose `DateRangeRequest` binds from the query string, unlike the Oura
/// range read which takes the same record in a POST body. That asymmetry is the server's.
struct WellnessAPI: Sendable {
    let client: APIClient

    func states(from: CalendarDay, to: CalendarDay) async throws -> [WellnessState] {
        try await client.get(
            "/api/wellness/get-range",
            query: [
                URLQueryItem(name: "startDate", value: "\(from.wire)T00:00:00"),
                URLQueryItem(name: "endDate", value: "\(to.wire)T00:00:00"),
            ],
            as: LenientList<WellnessState>.self).elements
    }

    /// The same range as a screen reads it, remembered under `key`.
    func states(
        from: CalendarDay, to: CalendarDay, remembering key: ReadKey
    ) async throws -> Remembered<[WellnessState]> {
        try await client.get(
            "/api/wellness/get-range",
            query: [
                URLQueryItem(name: "startDate", value: "\(from.wire)T00:00:00"),
                URLQueryItem(name: "endDate", value: "\(to.wire)T00:00:00"),
            ],
            as: LenientList<WellnessState>.self,
            remembering: key
        ).map(\.elements)
    }
}
