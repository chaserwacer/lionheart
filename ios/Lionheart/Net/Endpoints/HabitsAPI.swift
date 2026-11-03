import Foundation

/// The habits routes (`docs/api-surface.md` → "Habits"): a span's report, and one day's timeline.
struct HabitsAPI: Sendable {
    let client: APIClient

    func report(_ range: DayRange, remembering key: ReadKey) async throws -> Remembered<HabitsReport> {
        try await client.get(
            "/api/habits",
            query: [
                URLQueryItem(name: "From", value: range.from.wire),
                URLQueryItem(name: "To", value: range.to.wire),
            ],
            as: HabitsReport.self,
            remembering: key)
    }

    /// One day's timeline. A day with no activity stored answers 404, which is a fact about the
    /// day rather than a failure, so it reads as `nil`.
    func day(_ day: CalendarDay, remembering key: ReadKey) async throws -> Remembered<HabitsDayDetail?> {
        do {
            return try await client.get("/api/habits/days/\(day.wire)", as: HabitsDayDetail.self, remembering: key)
                .map { Optional($0) }
        } catch APIError.notFound {
            return .current(nil)
        }
    }
}
