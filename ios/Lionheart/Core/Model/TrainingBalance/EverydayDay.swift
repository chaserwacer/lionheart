import Foundation

/// One day's everyday load — the movement the ring caught outside training, in MET-minutes — and how
/// it stands against the athlete's usual day. Fatigue, never training credit.
struct EverydayDay: Equatable, Sendable {
    let day: CalendarDay
    let metMinutes: Double
    let standing: Standing

    enum Standing: Equatable, Sendable {
        /// Against his usual day (the median of the 90 complete days before) and the top of his
        /// ordinary days (their 90th percentile).
        case judged(usual: Double, high: Double, band: Band)
        /// Too few days before it to know his usual.
        case unjudged
        case unrecognised(String)
    }

    enum Band: Equatable, Sendable {
        case usual, above, farAbove
        case unrecognised(String)

        init(wire: String) {
            switch wire {
            case "usual": self = .usual
            case "above": self = .above
            case "farAbove": self = .farAbove
            default: self = .unrecognised(wire)
            }
        }
    }

    /// The day over his usual day, when there is a usual to compare it with.
    var ratio: Double? {
        guard case .judged(let usual, _, _) = standing, usual > 0 else { return nil }
        return metMinutes / usual
    }
}

extension EverydayDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "An everyday-load day needs its day."))
        }
        self.init(
            day: day,
            metMinutes: container.lenient("metMinutes", or: 0),
            standing: container.lenient(Standing.self, "standing") ?? .unrecognised(""))
    }
}

extension EverydayDay.Standing: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "judged":
            self = .judged(
                usual: container.lenient("usual", or: 0),
                high: container.lenient("high", or: 0),
                band: EverydayDay.Band(wire: container.lenient("band", or: "")))
        case "unjudged":
            self = .unjudged
        default:
            self = .unrecognised(type)
        }
    }
}
