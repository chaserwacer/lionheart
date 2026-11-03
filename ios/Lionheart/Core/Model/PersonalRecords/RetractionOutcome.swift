import Foundation

/// What became of a subject's record once the standing one was withdrawn.
///
/// Discriminated rather than a nullable record, because "your previous record stands again" and
/// "this subject now holds no record of that kind" are both successes the screen shows differently.
enum RetractionOutcome: Equatable, Sendable {
    case restored(PersonalRecord)
    case nothingStands
    /// A `$type` this build does not know. The retraction still happened — the server answered
    /// 200 — so it is a success the screen confirms by re-reading.
    case unrecognised(String)
}

extension RetractionOutcome: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "restored":
            self = container.lenient(PersonalRecord.self, "record").map(Self.restored)
                ?? .unrecognised(type)
        case "nothingStands":
            self = .nothingStands
        default:
            self = .unrecognised(type)
        }
    }
}
