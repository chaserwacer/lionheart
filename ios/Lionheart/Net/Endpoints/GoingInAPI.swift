import Foundation

/// The two reads the logging surface's header is built from (`docs/api-surface.md` → "Going-in
/// state"): how ready the athlete is now, and what his next lift and cardio should be.
struct GoingInAPI: Sendable {
    let client: APIClient

    /// The state now, on the athlete's clock — the server reads its own, so there is no day to send.
    func goingIn(remembering key: ReadKey) async throws -> Remembered<GoingInState> {
        try await client.get("/api/training/going-in", as: GoingInState.self, remembering: key)
    }

    func next(on day: CalendarDay, remembering key: ReadKey) async throws -> Remembered<NextSessions> {
        try await client.get("/api/training/next/\(day.wire)", as: NextSessions.self, remembering: key)
    }
}

extension ReadKey {
    static let goingIn = ReadKey("train.going-in")
    static let nextSessions = ReadKey("train.next-sessions")
}
