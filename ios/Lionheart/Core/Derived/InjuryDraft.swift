import Foundation

/// A new injury as the athlete describes it, before the server has it.
///
/// `make` is the only way to build one, so a draft that reaches the API always has a name and a
/// start that is not in the future — the two things the form can get wrong.
struct InjuryDraft: Equatable, Sendable {
    let name: String
    let startedOn: CalendarDay
    let notes: String

    /// Why the form cannot be sent yet.
    enum Problem: Error, Equatable, Sendable {
        case unnamed
        case startsInTheFuture
    }

    static func make(
        name: String, startedOn: CalendarDay, notes: String, today: CalendarDay
    ) -> Result<InjuryDraft, Problem> {
        guard let name = name.nonEmpty else { return .failure(.unnamed) }
        guard startedOn <= today else { return .failure(.startsInTheFuture) }
        return .success(InjuryDraft(name: name, startedOn: startedOn, notes: notes.nonEmpty ?? ""))
    }
}

/// A treatment or check-in about to be logged against an injury.
struct InjuryEventDraft: Equatable, Sendable {
    let kind: Kind
    let painLevel: Int
    let notes: String

    /// The two kinds the server accepts. `InjuryEvent.Kind` also has an unrecognised case for what
    /// a newer server sends back; nothing on this device can author one, so the draft has no room
    /// for it.
    enum Kind: Hashable, Sendable, CaseIterable, Identifiable {
        case checkIn
        case treatment

        var id: Self { self }

        var label: String {
            switch self {
            case .checkIn: "Check-in"
            case .treatment: "Treatment"
            }
        }

        /// The server's `InjuryEventType`, which crosses the wire as its integer.
        var wire: Int {
            switch self {
            case .treatment: 0
            case .checkIn: 1
            }
        }
    }

    /// The pain scale the server validates against.
    static let painScale = 0...10

    /// The draft, with the pain level held to the scale and the notes trimmed.
    init(kind: Kind, painLevel: Int, notes: String) {
        self.kind = kind
        self.painLevel = min(max(painLevel, Self.painScale.lowerBound), Self.painScale.upperBound)
        self.notes = notes.nonEmpty ?? ""
    }
}
