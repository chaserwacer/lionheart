import Foundation

/// The purpose-built command bodies the session routes take.
///
/// One file rather than one per type, mirroring the server's own `TrainingSessionRequests.cs`:
/// they are a set of sibling records that only make sense next to the route table, and reading
/// them together is how you check that none of them states something it may not.
///
/// None carries a session id, a user id, a provenance or a decoration. The server owns all four,
/// and a client able to state them could author an AI-provenance session or inject a decoration
/// into the projection every analysis reads. `trainingProgramId` is a flat optional rather than
/// membership's two-case fill-state, which is the shape the server asks for.
///
/// Every instant here is a `DateTimeOffset` the server derives a calendar day from, so every one
/// is written with `WireDate.offsetWire(from:)` — see that function for what sending UTC instead
/// would quietly do.
struct StartSessionRequest: Encodable, Sendable {
    let startedAt: Date

    private enum CodingKeys: String, CodingKey { case startedAt }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(WireDate.offsetWire(from: startedAt), forKey: .startedAt)
    }
}

/// `POST /active` — the athlete simply began training.
struct StartUnplannedSessionRequest: Encodable, Sendable {
    let startedAt: Date
    let notes: String
    let trainingProgramId: UUID?

    private enum CodingKeys: String, CodingKey {
        case startedAt, notes, trainingProgramId
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(WireDate.offsetWire(from: startedAt), forKey: .startedAt)
        try container.encode(notes, forKey: .notes)
        try container.encode(trainingProgramId, forKey: .trainingProgramId)
    }
}

/// `POST /completed` — the whole session in one request, which is how a workout logged after the
/// fact reaches the server. The device holds it locally until then, so this is also the one write
/// in the training surface that works having never had a connection.
struct LogCompletedSessionRequest: Encodable, Sendable {
    let sets: [SetCommand]
    let period: PeriodRequest
    let effort: PerceivedEffortRequest?
    let notes: String
    let trainingProgramId: UUID?
}

/// `POST /{id}/complete` — Active → Completed.
struct CompleteSessionRequest: Encodable, Sendable {
    let period: PeriodRequest
    let effort: PerceivedEffortRequest?
}

/// `PUT /{id}/sets` — replaces the whole ordered list. There is deliberately no per-set route:
/// the sets persist as one JSON column, so a whole-list write is what happens at the storage layer
/// however the route is shaped.
struct ReplaceSetsRequest: Encodable, Sendable {
    let sets: [SetCommand]
}

/// When something happened, as a client states it.
///
/// Flat `start` and `end` — **not** the polymorphic `$type` shape the same concept comes back in.
/// The asymmetry is the server's and is deliberate on its side: an `Interval` whose end precedes
/// its start throws, and a throw inside the input formatter escapes as a 500, so the ordering is
/// checked one step earlier where it can be a 400. A missing `end` is an instant, not a zero-length
/// interval.
struct PeriodRequest: Encodable, Sendable {
    let start: Date
    let end: Date?

    private enum CodingKeys: String, CodingKey { case start, end }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(WireDate.offsetWire(from: start), forKey: .start)
        try container.encodeIfPresent(end.map { WireDate.offsetWire(from: $0) }, forKey: .end)
    }

    /// The period a workout occupied, from when it started to when it was finished.
    static func interval(from start: Date, to end: Date) -> PeriodRequest {
        // A finish that lands before the start — a clock that moved backwards — would be refused
        // with a 400 the athlete cannot act on, so it collapses to the instant instead.
        end < start ? PeriodRequest(start: start, end: nil) : PeriodRequest(start: start, end: end)
    }
}

/// Perceived effort as the athlete may state it.
///
/// Every rating is optional because rating some and skipping the rest is normal. `recordedAt` is
/// absent on purpose: when the ratings were written down is the server's bookkeeping, not a fact
/// the client is authorized to assert. An instance with nothing in it means the same thing as
/// sending none at all, which the server settles rather than the client.
struct PerceivedEffortRequest: Codable, Sendable, Equatable {
    let accumulatedFatigue: Int?
    let difficultyRating: Int?
    let engagementRating: Int?
    let externalVariablesRating: Int?

    static let unrated = PerceivedEffortRequest(
        accumulatedFatigue: nil, difficultyRating: nil,
        engagementRating: nil, externalVariablesRating: nil)

    var isEmpty: Bool { self == .unrated }
}
