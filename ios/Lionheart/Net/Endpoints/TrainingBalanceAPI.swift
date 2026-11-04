import Foundation

/// Weekly volume and the training score (`docs/api-surface.md` → "Movement patterns and volume",
/// "Training score").
///
/// Each read names the last day of its seven, which is the athlete's calendar day: the server has no
/// zone to read "today" in.
struct TrainingBalanceAPI: Sendable {
    let client: APIClient

    func volume(endingOn day: CalendarDay, remembering key: ReadKey) async throws -> Remembered<TrainingVolume> {
        try await client.get("/api/training/volume/\(day.wire)", as: TrainingVolume.self, remembering: key)
    }

    func score(endingOn day: CalendarDay, remembering key: ReadKey) async throws -> Remembered<TrainingScore> {
        try await client.get("/api/training/score/\(day.wire)", as: TrainingScore.self, remembering: key)
    }

    /// The next lift, the next cardio and which first, as of the day (`GET /api/training/next/{day}`).
    func next(asOf day: CalendarDay, remembering key: ReadKey) async throws -> Remembered<NextSessions> {
        try await client.get("/api/training/next/\(day.wire)", as: NextSessions.self, remembering: key)
    }

    /// One score per Sunday in the range, oldest first; the server scores at most 48.
    func weeks(_ range: DayRange, remembering key: ReadKey) async throws -> Remembered<[WeeklyScore]> {
        try await client.get(
            "/api/training/score/weeks",
            query: [
                URLQueryItem(name: "From", value: range.from.wire),
                URLQueryItem(name: "To", value: range.to.wire),
            ],
            as: LenientList<WeeklyScore>.self,
            remembering: key
        ).map(\.elements)
    }
}
