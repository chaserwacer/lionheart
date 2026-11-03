import Foundation

/// When something happened: one moment, or a span.
///
/// The server holds both halves of a moment deliberately — the absolute instant for ordering, and
/// the calendar day read at the offset it was logged at — and sends the day separately rather than
/// letting a client re-derive it. So this type carries only the instants; a session's day comes
/// from its own `date`, never from arithmetic on a period.
enum Period: Equatable, Sendable {
    case instant(at: Date)
    case interval(start: Date, end: Date)
    /// A `$type` this client does not know, or a payload whose instants did not parse. Kept as a
    /// case so a session carrying one still renders everything else about itself.
    case unrecognised(String)

    /// Where the period begins, for ordering. Absent only for `.unrecognised`, which has no
    /// instant to offer.
    var startTime: Date? {
        switch self {
        case .instant(let at): at
        case .interval(let start, _): start
        case .unrecognised: nil
        }
    }

    /// How long it lasted. An instant has no duration — that is a different fact from a duration
    /// of zero, so it is `nil` rather than `0`.
    var duration: TimeInterval? {
        switch self {
        case .instant, .unrecognised: nil
        case .interval(let start, let end): end.timeIntervalSince(start)
        }
    }
}

extension Period: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "instant":
            guard let at = container.instant("at") else { self = .unrecognised(type); return }
            self = .instant(at: at)
        case "interval":
            guard let start = container.instant("start"), let end = container.instant("end") else {
                self = .unrecognised(type)
                return
            }
            self = .interval(start: start, end: end)
        default:
            self = .unrecognised(type)
        }
    }
}
