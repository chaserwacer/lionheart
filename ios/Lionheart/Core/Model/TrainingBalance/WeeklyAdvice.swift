import Foundation

/// The one piece of advice a calendar week carries — the server's `WeeklyAdvice` on the week's
/// expectations.
///
/// Chosen by the server's fixed rules from the weeks before the Monday, so it reads the same every
/// day of the week and the chat is handed the same words. The client draws it; the kind only picks
/// the symbol beside it.
struct WeeklyAdvice: Equatable, Sendable {
    let kind: Kind
    /// The Monday of the week it is for.
    let weekStarting: CalendarDay
    let headline: String
    /// Why, from the weeks it was read from.
    let reasons: [String]

    enum Kind: Equatable, Sendable {
        case manageInjury
        case takeDeload
        case easeRamp
        case easeAfterEverydayLoad
        case emphasiseCardio
        case emphasiseLifting
        case priorityStalled
        case keepBuilding
        case unrecognised(String)

        init(wire: String) {
            switch wire {
            case "manageInjury": self = .manageInjury
            case "takeDeload": self = .takeDeload
            case "easeRamp": self = .easeRamp
            case "easeAfterEverydayLoad": self = .easeAfterEverydayLoad
            case "emphasiseCardio": self = .emphasiseCardio
            case "emphasiseLifting": self = .emphasiseLifting
            case "priorityStalled": self = .priorityStalled
            case "keepBuilding": self = .keepBuilding
            default: self = .unrecognised(wire)
            }
        }
    }
}

extension WeeklyAdvice: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let headline = container.lenient(String.self, "headline")?.nonEmpty,
              let weekStarting = container.day("weekStarting")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "Advice needs its week and its headline."))
        }
        self.init(
            kind: Kind(wire: container.wireType),
            weekStarting: weekStarting,
            headline: headline,
            reasons: container.lenientList(String.self, "reasons"))
    }
}
