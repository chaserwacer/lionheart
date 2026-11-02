import Foundation

/// The Oura routes.
///
/// These predate the remodel and are not in `docs/api-surface.md`; the shapes here were read off
/// the endpoints themselves (`Endpoints/OuraEndpoints/`), which is why the paths are verbs rather
/// than resources.
///
/// **There is no "latest readiness" route, and `GET /get-daily-oura-data` is the wrong way to
/// approximate one.** That route answers 200 with a *synthesized placeholder* when the day has no
/// data — a zero readiness score and an empty object id — so a client asking for today cannot tell
/// "not recovered" from "the ring has not synced". The range route returns only rows that actually
/// exist, so asking for a window and taking the most recent day is the only read that can say
/// "no data" honestly. `Core/Derived/` does the picking.
struct OuraAPI: Sendable {
    let client: APIClient

    /// Every stored Oura day in the window, in no guaranteed order.
    func days(from: CalendarDay, to: CalendarDay) async throws -> [OuraDay] {
        try await client.post(
            "/api/oura/get-daily-oura-data-range",
            body: DateRangeBody(startDate: from, endDate: to),
            as: LenientList<OuraDay>.self).elements
    }
}

/// `DateRangeRequest` as the server binds it: `DateTime` members, so a day is sent as midnight.
///
/// The server immediately narrows both to `DateOnly`, so the time of day is discarded there and
/// midnight is the honest thing to send rather than a value pretending to be an instant.
struct DateRangeBody: Encodable, Sendable {
    let startDate: CalendarDay
    let endDate: CalendarDay

    private enum CodingKeys: String, CodingKey {
        case startDate
        case endDate
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode("\(startDate.wire)T00:00:00", forKey: .startDate)
        try container.encode("\(endDate.wire)T00:00:00", forKey: .endDate)
    }
}
