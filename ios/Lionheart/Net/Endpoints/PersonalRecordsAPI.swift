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
    /// Remembered under `key`.
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

    /// Every record a movement established, in every standing, newest first — what marks the sets
    /// of its history that set one.
    func established(on movementDataId: UUID) async throws -> Remembered<[PersonalRecord]> {
        try await client.get(
            "\(Self.root)/established",
            query: [URLQueryItem(name: "movementDataId", value: movementDataId.uuidString)],
            as: LenientList<PersonalRecord>.self,
            remembering: .establishedRecords(movement: movementDataId)
        ).map(\.elements)
    }

    /// Every record established inside a window of instants, in every standing.
    ///
    /// A record is dated with its session's start, so a session's own records are the window over
    /// its period.
    func established(from start: Date, to end: Date, remembering key: ReadKey) async throws
        -> Remembered<[PersonalRecord]>
    {
        try await client.get(
            "\(Self.root)/established",
            query: [
                URLQueryItem(name: "from", value: WireDate.wire(from: start)),
                URLQueryItem(name: "to", value: WireDate.wire(from: end)),
            ],
            as: LenientList<PersonalRecord>.self,
            remembering: key
        ).map(\.elements)
    }

    /// Withdraws a standing record, putting back whatever it beat. A record that no longer stands
    /// answers 409 with the server's own sentence, which arrives as `APIError.refused`.
    func retract(_ personalRecordId: UUID) async throws -> RetractionOutcome {
        try await client.post("\(Self.root)/\(personalRecordId.uuidString)/retract")
    }
}
