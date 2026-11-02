import Foundation

/// How an entry was logged: typed in words, or formed through the UI.
///
/// Structuring is additive on the server — a texted source survives any later upgrade to a richer
/// set shape — so the raw words are worth carrying to the device even when the set itself is fully
/// structured. The quick-log receipt is the first screen that shows them.
enum Utterance: Equatable, Sendable {
    case formed
    case texted(rawText: String, structureConfidence: Double)
    case unrecognised(String)

    var rawText: String? {
        switch self {
        case .formed, .unrecognised: nil
        case .texted(let rawText, _): rawText
        }
    }
}

extension Utterance: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "formed":
            self = .formed
        case "texted":
            self = .texted(
                rawText: container.lenient("rawText", or: ""),
                structureConfidence: container.lenient("structureConfidence", or: 0))
        default:
            self = .unrecognised(type)
        }
    }
}
