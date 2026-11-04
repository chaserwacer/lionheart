import Foundation

/// The wellness routes, read off `Endpoints/WellnessEndpoints/` — they predate the remodel, and
/// `docs/api-surface.md` → "Wellness" records them as they are.
///
/// The range read is a `GET` whose `DateRangeRequest` binds from the query string, unlike the Oura
/// range read which takes the same record in a POST body. That asymmetry is the server's.
struct WellnessAPI: Sendable {
    let client: APIClient

    /// The entries in the range, remembered under `key`.
    func states(
        from: CalendarDay, to: CalendarDay, remembering key: ReadKey
    ) async throws -> Remembered<[WellnessState]> {
        try await client.get(
            "/api/wellness/get-range",
            query: [
                URLQueryItem(name: "startDate", value: Self.midnight(from)),
                URLQueryItem(name: "endDate", value: Self.midnight(to)),
            ],
            as: LenientList<WellnessState>.self,
            remembering: key
        ).map(\.elements)
    }

    /// Logs the day's ratings, replacing the day's entry where there is one — the server keeps one
    /// per day, and answers 201 for a new day and 200 for a replaced one, both with the state.
    func add(_ entry: WellnessEntry) async throws -> WellnessState {
        try await client.post("/api/wellness/add", body: AddWellnessStateRequest(entry))
    }

    /// The server binds a `DateTime` and keys on its calendar day, so a day is sent as its midnight
    /// with no offset — read in the server's own zone as that same day.
    static func midnight(_ day: CalendarDay) -> String { "\(day.wire)T00:00:00" }
}

/// `POST /api/wellness/add` — one day's four ratings.
struct AddWellnessStateRequest: Encodable, Sendable {
    let date: String
    let energy: Int
    let motivation: Int
    let mood: Int
    let stress: Int

    init(_ entry: WellnessEntry) {
        date = WellnessAPI.midnight(entry.day)
        energy = entry.energy
        motivation = entry.motivation
        mood = entry.mood
        stress = entry.stress
    }
}
