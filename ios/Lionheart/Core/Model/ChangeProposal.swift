import Foundation

/// A change Tempo proposed in the thread, as the athlete is shown it on a card, and where it stands
/// (`docs/api-surface.md` → "Proposed changes").
///
/// Only what the card shows crosses the wire. The change itself stays on the server, and the card is
/// answered by id, so a confirmation can only ever write what the athlete read.
struct ChangeProposal: Equatable, Sendable, Identifiable {
    let changeProposalId: UUID
    let proposedAt: Date
    let area: ChangeArea
    let title: String
    let lines: [String]
    let answer: ProposalAnswer

    var id: UUID { changeProposalId }
}

/// The part of the app a confirmed change lands in: the screens that read again once it is written.
enum ChangeArea: Equatable, Sendable {
    /// Planned sessions: Train, Today, availability and the week's targets.
    case plan
    /// Non-training days: availability and every target prorated to it.
    case availability
    /// Focus pins: the priorities and their aims.
    case focus
    /// Injuries and their check-ins.
    case injuries
    case unrecognised(String)

    init(wire: String?) {
        switch wire {
        case "plan": self = .plan
        case "availability": self = .availability
        case "focus": self = .focus
        case "injuries": self = .injuries
        default: self = .unrecognised(wire ?? "")
        }
    }
}

/// Where a proposal stands: waiting for the athlete, or answered one of three ways.
enum ProposalAnswer: Equatable, Sendable {
    case waiting
    case confirmed(at: Date?)
    case declined(at: Date?)
    /// The athlete said yes and the service that would have written it said no, for this reason.
    case refused(at: Date?, reason: String)
    case unrecognised(String)

    /// Whether the card still asks for an answer.
    var isWaiting: Bool {
        switch self {
        case .waiting: true
        case .confirmed, .declined, .refused, .unrecognised: false
        }
    }
}

// MARK: - Decoding

extension ChangeProposal: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        // A card with no id cannot be answered, and one with no title has nothing to say.
        guard let changeProposalId = container.lenient(UUID.self, "changeProposalId"),
              let title = container.lenient(String.self, "title")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A proposal needs its id and its title."))
        }

        self.init(
            changeProposalId: changeProposalId,
            proposedAt: container.instant("proposedAt") ?? .distantPast,
            area: ChangeArea(wire: container.lenient(String.self, "area")),
            title: title,
            lines: container.lenient("lines", or: []),
            answer: container.lenient(ProposalAnswer.self, "answer") ?? .unrecognised(""))
    }
}

extension ProposalAnswer: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "waiting":
            self = .waiting
        case "confirmed":
            self = .confirmed(at: container.instant("at"))
        case "declined":
            self = .declined(at: container.instant("at"))
        case "refused":
            self = .refused(at: container.instant("at"), reason: container.lenient("reason", or: ""))
        default:
            self = .unrecognised(type)
        }
    }
}
