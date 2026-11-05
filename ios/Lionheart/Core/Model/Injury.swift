import Foundation

/// A tracked problem, the families it touches, and the events logged against it over time.
struct Injury: Equatable, Sendable, Identifiable {
    let injuryId: UUID
    let name: String
    let notes: String
    let injuryDate: CalendarDay
    let isActive: Bool
    /// The families the athlete said it touches, which pause while it is active; empty when not said.
    let families: [PatternFamily]
    let events: [InjuryEvent]

    var id: UUID { injuryId }
}

/// One occurrence of experiencing or treating an injury.
struct InjuryEvent: Equatable, Sendable, Identifiable {
    let injuryEventId: UUID
    let trainingSessionId: UUID?
    let notes: String
    /// 0–10 as the server validates it.
    let painLevel: Int
    let kind: Kind
    let createdAt: Date?

    var id: UUID { injuryEventId }

    /// Integer-valued on the wire; the order is the server's.
    enum Kind: Equatable, Sendable {
        case treatment
        case checkIn
        case unrecognised(Int)

        init(wire: Int?) {
            switch wire {
            case 0: self = .treatment
            case 1: self = .checkIn
            default: self = .unrecognised(wire ?? -1)
            }
        }
    }
}

extension Injury: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let injuryId = container.lenient(UUID.self, "injuryID") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "An injury without an id cannot be acted on."))
        }

        self.init(
            injuryId: injuryId,
            name: container.lenient("name", or: ""),
            notes: container.lenient("notes", or: ""),
            injuryDate: container.day("injuryDate") ?? CalendarDay(Date()),
            isActive: container.lenient("isActive", or: false),
            families: container.lenientList(String.self, "families").map(PatternFamily.init(wire:)),
            events: container.lenientList(InjuryEvent.self, "injuryEvents"))
    }
}

extension InjuryEvent: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let injuryEventId = container.lenient(UUID.self, "injuryEventID") else {
            // Minting one here would give the event a different identity on every refresh, which
            // is worse than dropping it: the list would animate as though the row had been
            // replaced. The update and delete routes need the real id anyway.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "An injury event without an id cannot be acted on."))
        }

        self.init(
            injuryEventId: injuryEventId,
            trainingSessionId: container.lenient(UUID.self, "trainingSessionID"),
            notes: container.lenient("notes", or: ""),
            painLevel: container.lenient("painLevel", or: 0),
            kind: Kind(wire: container.lenient(Int.self, "injuryType")),
            createdAt: container.instant("creationTime"))
    }
}
