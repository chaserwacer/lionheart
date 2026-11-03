import Foundation

/// The Oura routes.
///
/// Two shapes, because Oura's collections are two shapes. Six describe a calendar day and arrive
/// together as one `OuraDay`; four are events inside a day — zero to many each, occupying clock
/// time — and each has its own route returning the same envelope around a different reading.
///
/// **Absence is honest on every one of these.** A collection the ring filed nothing for arrives
/// null on the day; a day it recorded nothing for is absent from the range, and asking for it by
/// date answers 404. So no read here needs to second-guess a value: a zero is a zero Oura issued.
struct OuraAPI: Sendable {
    let client: APIClient

    /// Every day in the range the ring recorded something for, oldest first.
    ///
    /// Days with nothing stored are simply missing from the answer rather than present and empty,
    /// which is what lets a caller tell "a bad night" from "the ring was in a drawer".
    func days(from: CalendarDay, to: CalendarDay) async throws -> [OuraDay] {
        try await client.get(
            "/api/oura/days",
            query: [
                URLQueryItem(name: "From", value: from.wire),
                URLQueryItem(name: "To", value: to.wire),
            ],
            as: LenientList<OuraDay>.self).elements
    }

    /// How far the stored data reaches: the newest day any collection covers.
    ///
    /// The route answers 404 when nothing is stored at all, which is a fact about the store rather
    /// than a failure, so it is read here as `.nothingStored`.
    func coverage() async throws -> OuraCoverage {
        do {
            let latest = try await client.get("/api/oura/days/latest", as: OuraDay.self)
            return .through(latest.date)
        } catch APIError.notFound {
            return .nothingStored
        }
    }

    /// Pulls every collection for `range` from Oura and stores what came back.
    ///
    /// A rejected Oura token answers 502 with a detail naming the token, not 401 — a 401 here would
    /// be this app's own session. `OuraSyncFailure.from` restates both.
    func sync(_ range: DayRange) async throws -> OuraSyncReport {
        try await client.post("/api/oura/sync", body: range, as: OuraSyncReport.self)
    }

    func sleepPeriods(from: Date, to: Date) async throws -> [OuraEvent<OuraSleepPeriod>] {
        try await events("/api/oura/sleep", from: from, to: to)
    }

    func workouts(from: Date, to: Date) async throws -> [OuraEvent<OuraWorkout>] {
        try await events("/api/oura/workouts", from: from, to: to)
    }

    func tags(from: Date, to: Date) async throws -> [OuraEvent<OuraTag>] {
        try await events("/api/oura/tags", from: from, to: to)
    }

    /// The event routes differ only in their path and their reading, so they share this.
    ///
    /// The window binds as instants rather than days, because an event occupies clock time — which
    /// is also why a night beginning before midnight can fall outside a window its day is inside.
    private func events<Reading: Decodable & Equatable & Sendable>(
        _ path: String, from: Date, to: Date
    ) async throws -> [OuraEvent<Reading>] {
        try await client.get(
            path,
            query: [
                URLQueryItem(name: "From", value: WireDate.wire(from: from)),
                URLQueryItem(name: "To", value: WireDate.wire(from: to)),
            ],
            as: LenientList<OuraEvent<Reading>>.self).elements
    }
}
