import Foundation

/// Where a run of the interpretation engine ended.
///
/// **Both cases are successes.** Being asked which movement a name meant is how a request that was
/// understood ended, not a reason it was refused — the server answers 200 with the case named in
/// the body, and treating `.awaitingConfirmation` as a failure is the mistake this type exists to
/// prevent.
///
/// The draft travels as an undecoded `JSONValue` on purpose: resuming means handing the server back
/// the draft it sent, and mirroring the parser's whole output in Swift types would turn every
/// member of it into something this client could silently drop. See `JSONValue`.
enum InterpretationOutcome: Equatable, Sendable {
    case committed(Committed)
    case awaitingConfirmation(AwaitingConfirmation)
    case unrecognised(String)

    struct Committed: Equatable, Sendable {
        let sessionIds: [UUID]
        let contextIds: [UUID]
        let warnings: [String]
    }

    struct AwaitingConfirmation: Equatable, Sendable {
        /// The names the parser could not pin down, each with the candidates it found.
        let references: [UnresolvedReference]
        /// The parsed draft exactly as it arrived, for echoing back to `POST /confirm`.
        let draft: JSONValue
    }
}

/// A name the parser used that resolve could not map onto one entity.
struct UnresolvedReference: Equatable, Sendable, Identifiable {
    let rawName: String
    let kind: Kind
    let candidates: [EntityMatch]

    var id: String { "\(rawName)#\(kind)" }

    /// What sort of entity the name was standing for. Integer-valued on the wire.
    enum Kind: Equatable, Sendable, Hashable {
        case movementBase
        case equipment
        case unrecognised(Int)

        init(wire: Int?) {
            switch wire {
            case 0: self = .movementBase
            case 1: self = .equipment
            default: self = .unrecognised(wire ?? -1)
            }
        }
    }
}

/// One candidate entity for an unresolved name, with how close the match was.
struct EntityMatch: Equatable, Sendable, Identifiable {
    let entityId: UUID
    let name: String
    let confidence: Double

    var id: UUID { entityId }
}

extension InterpretationOutcome: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "committed":
            self = .committed(Committed(
                sessionIds: container.lenientList(UUID.self, "sessionIds"),
                contextIds: container.lenientList(UUID.self, "contextIds"),
                warnings: container.lenientList(String.self, "warnings")))

        case "awaitingConfirmation":
            self = .awaitingConfirmation(AwaitingConfirmation(
                references: container.lenientList(UnresolvedReference.self, "references"),
                draft: container.lenient(JSONValue.self, "draft") ?? .null))

        default:
            self = .unrecognised(type)
        }
    }
}

extension UnresolvedReference: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            rawName: container.lenient("rawName", or: ""),
            kind: Kind(wire: container.lenient(Int.self, "kind")),
            candidates: container.lenientList(EntityMatch.self, "candidates"))
    }
}

extension EntityMatch: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            entityId: container.lenient(UUID.self, "entityId") ?? UUID(),
            name: container.lenient("name", or: ""),
            confidence: container.lenient("confidence", or: 0))
    }
}
