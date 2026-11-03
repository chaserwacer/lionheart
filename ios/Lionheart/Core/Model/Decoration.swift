import Foundation

/// One closed-vocabulary label on a fixed axis. The axes are fixed; the values grow, which is why
/// `value` stays a string while `axis` is an enum.
struct Facet: Equatable, Sendable, Decodable {
    let axis: FacetAxis
    let value: String

    init(axis: FacetAxis, value: String) {
        self.axis = axis
        self.value = value
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            axis: FacetAxis(wire: container.lenient(Int.self, "axis")),
            value: container.lenient("value", or: ""))
    }
}

/// The fixed set of analysis axes. Integer-valued on the wire; the order is the server's.
enum FacetAxis: Equatable, Sendable {
    case modality
    case movementPattern
    case muscleRegion
    case intent
    case lifeDomain
    case bodyState
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .modality
        case 1: self = .movementPattern
        case 2: self = .muscleRegion
        case 3: self = .intent
        case 4: self = .lifeDomain
        case 5: self = .bodyState
        default: self = .unrecognised(wire ?? -1)
        }
    }
}

/// Whether an entry reads positive, negative, neutral or mixed.
enum Valence: Equatable, Sendable {
    case positive
    case negative
    case neutral
    case mixed
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .positive
        case 1: self = .negative
        case 2: self = .neutral
        case 3: self = .mixed
        default: self = .unrecognised(wire ?? -1)
        }
    }
}

/// Whether an analyzable entry has been annotated yet.
///
/// Sessions are decorated on completion and contexts at creation, so a session the athlete has
/// only just finished legitimately arrives `.none`. That is a state the UI shows differently from
/// a decoration with nothing in it, which is why it is a case and not an empty `Decoration`.
enum DecorationState: Equatable, Sendable {
    case none
    case decorated(facets: [Facet], freeTags: [String], valence: Valence)
    case unrecognised(String)

    var facets: [Facet] {
        switch self {
        case .none, .unrecognised: []
        case .decorated(let facets, _, _): facets
        }
    }
}

extension DecorationState: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "none":
            self = .none
        case "decorated":
            self = .decorated(
                facets: container.lenientList(Facet.self, "facets"),
                freeTags: container.lenientList(String.self, "freeTags"),
                valence: Valence(wire: container.lenient(Int.self, "valence")))
        default:
            self = .unrecognised(type)
        }
    }
}
