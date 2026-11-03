import Foundation

/// The athlete's own read on a session, as a cluster.
///
/// Every rating is individually optional on the server, so this type says nothing about which of
/// them were given — `isEmpty` is what tells a screen whether there is anything to show.
struct PerceivedEffortRatings: Equatable, Sendable {
    let recordedAt: Date?
    let accumulatedFatigue: Int?
    let difficultyRating: Int?
    let engagementRating: Int?
    let externalVariablesRating: Int?

    var isEmpty: Bool {
        accumulatedFatigue == nil && difficultyRating == nil
            && engagementRating == nil && externalVariablesRating == nil
    }
}

extension PerceivedEffortRatings: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            recordedAt: container.instant("recordedAt"),
            accumulatedFatigue: container.lenient(Int.self, "accumulatedFatigue"),
            difficultyRating: container.lenient(Int.self, "difficultyRating"),
            engagementRating: container.lenient(Int.self, "engagementRating"),
            externalVariablesRating: container.lenient(Int.self, "externalVariablesRating"))
    }
}

/// Whether effort was reported at all.
///
/// A case rather than an optional `PerceivedEffortRatings`, because "the athlete did not rate this
/// session" is a thing a screen says, and it is not the same as ratings that happen to be blank.
enum EffortState: Equatable, Sendable {
    case notReported
    case reported(PerceivedEffortRatings)
    case unrecognised(String)

    var ratings: PerceivedEffortRatings? {
        switch self {
        case .notReported, .unrecognised: nil
        case .reported(let ratings): ratings
        }
    }
}

extension EffortState: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "none":
            self = .notReported
        case "reported":
            guard let ratings = container.lenient(PerceivedEffortRatings.self, "ratings") else {
                self = .notReported
                return
            }
            self = .reported(ratings)
        default:
            self = .unrecognised(type)
        }
    }
}
