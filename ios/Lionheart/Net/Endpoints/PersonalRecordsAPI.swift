import Foundation

/// The personal-record routes, read off `Endpoints/Training/PersonalRecord/`.
///
/// **Nothing on the server writes a `PersonalRecord` today.** `PersonalRecordService` is read-only
/// and the completed-session route does not attempt a record, so these routes answer `[]` on any
/// database the dropped legacy training model never populated. The client is built against them as
/// specified rather than around that gap — when the write path lands, the records screen fills
/// itself — and that screen states the emptiness as "no records yet" rather than implying the
/// athlete has never set one.
struct PersonalRecordsAPI: Sendable {
    let client: APIClient

    /// Each movement's currently-standing records, both kinds side by side.
    ///
    /// The grouped read rather than the flat one: a records screen asks "what are my numbers on
    /// the bench press", and the flat list would make every screen re-group it.
    func recordsByMovement() async throws -> [MovementRecords] {
        try await client.get(
            "/api/personal-records/summaries", as: LenientList<MovementRecords>.self).elements
    }
}
