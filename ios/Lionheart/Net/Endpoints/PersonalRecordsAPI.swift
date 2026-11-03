import Foundation

/// The personal-record routes (`docs/api-surface.md` → Personal records).
///
/// Records are never created through a route: the server establishes them when a session's sets
/// become final. The only write is the athlete withdrawing one.
struct PersonalRecordsAPI: Sendable {
    let client: APIClient

    private static let root = "/api/personal-records"

    /// The standing records grouped by subject — each movement, and trail running.
    ///
    /// The grouped read rather than the flat one: every screen that reads records asks "what are
    /// my numbers on this movement", and the flat list would make each of them re-group it.
    func summaries() async throws -> [SubjectRecords] {
        try await client.get("\(Self.root)/summaries", as: LenientList<SubjectRecords>.self).elements
    }

    /// The summaries as a screen reads them, remembered under `key`.
    func summaries(remembering key: ReadKey) async throws -> Remembered<[SubjectRecords]> {
        try await client.get(
            "\(Self.root)/summaries", as: LenientList<SubjectRecords>.self, remembering: key
        ).map(\.elements)
    }

    /// Every record ever set for one subject and kind, newest first, beaten and withdrawn included,
    /// remembered so a chain once opened can be read again with no signal.
    ///
    /// The route takes no unit and answers every unit's chain of the kind; `RecordHistory` narrows
    /// it to one key.
    func history(of key: RecordKey) async throws -> Remembered<[PersonalRecord]> {
        let subject = key.subject.movementDataId.map {
            [URLQueryItem(name: "movementDataId", value: $0.uuidString)]
        } ?? []
        let bracket = key.kind.bracket.map { [URLQueryItem(name: "bracket", value: String($0))] } ?? []

        return try await client.get(
            "\(Self.root)/history",
            query: subject + [URLQueryItem(name: "kind", value: key.kind.wireName)] + bracket,
            as: LenientList<PersonalRecord>.self,
            remembering: .recordHistory(key)
        ).map(\.elements)
    }

    /// Withdraws a standing record, putting back whatever it beat. A record that no longer stands
    /// answers 409 with the server's own sentence, which arrives as `APIError.refused`.
    func retract(_ personalRecordId: UUID) async throws -> RetractionOutcome {
        try await client.post("\(Self.root)/\(personalRecordId.uuidString)/retract")
    }
}
