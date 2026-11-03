import Foundation

/// The interpretation engine's routes — the quick-log path.
///
/// `POST /interpret` answers **200 for both outcomes**: committing the entry, and needing to know
/// which movement a name meant. A caller that treats `awaitingConfirmation` as an error has
/// misread the contract; see `InterpretationOutcome`.
///
/// `POST /confirm` is not written here. Resuming needs the parsed draft echoed back unchanged
/// alongside the athlete's choices, and the screen that collects those choices does not exist yet —
/// `InterpretationOutcome.AwaitingConfirmation` already carries the draft for whoever builds it.
struct InterpretationAPI: Sendable {
    let client: APIClient

    func interpret(rawText: String, loggedAt: Date) async throws -> InterpretationOutcome {
        try await client.post(
            "/api/interpretation/interpret",
            body: InterpretationRequestBody(rawText: rawText, loggedAt: loggedAt))
    }
}

/// `InterpretationRequest`: the words, and when they happened.
///
/// `LoggedAt` is a `DateTime` on the server, so it is sent without an offset. It is written in
/// **local** time deliberately: it is what the parser reads relative expressions like "this
/// morning" against, and converting to UTC first would move an evening log to tomorrow.
struct InterpretationRequestBody: Encodable, Sendable {
    let rawText: String
    let loggedAt: Date

    private enum CodingKeys: String, CodingKey {
        case rawText
        case loggedAt
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(rawText, forKey: .rawText)
        try container.encode(Self.localWire(from: loggedAt), forKey: .loggedAt)
    }

    static func localWire(from date: Date) -> String {
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd'T'HH:mm:ss"
        return formatter.string(from: date)
    }
}
