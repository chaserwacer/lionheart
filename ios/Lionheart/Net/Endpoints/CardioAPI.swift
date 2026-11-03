import Foundation

/// The cardio report, `GET /api/cardio` (`docs/api-surface.md` → "Cardio").
///
/// One route for the whole screen: sessions, weeks, Oura's estimates and the aerobic-base
/// comparison arrive together because they are read from the same sessions and must agree.
struct CardioAPI: Sendable {
    let client: APIClient

    func report(_ range: DayRange, remembering key: ReadKey) async throws -> Remembered<CardioReport> {
        try await client.get(
            "/api/cardio",
            query: [
                URLQueryItem(name: "From", value: range.from.wire),
                URLQueryItem(name: "To", value: range.to.wire),
            ],
            as: CardioReport.self,
            remembering: key)
    }
}
