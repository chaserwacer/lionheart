import Foundation

/// Perceived effort as the athlete may state it.
///
/// Every rating is optional because rating some and skipping the rest is normal. `recordedAt` is
/// absent on purpose: when the ratings were written down is the server's bookkeeping, not a fact
/// the client is authorized to assert. An instance with nothing in it means the same thing as
/// sending none at all, which the server settles rather than the client.
///
/// It is the body `POST /{id}/complete` and `POST /completed` carry, and it is also what the
/// in-flight workout holds: the athlete can rate the session while it is going, and the ratings
/// ride in the workout document until the session is completed.
struct PerceivedEffortRequest: Codable, Sendable, Equatable {
    let accumulatedFatigue: Int?
    let difficultyRating: Int?
    let engagementRating: Int?
    let externalVariablesRating: Int?

    static let unrated = PerceivedEffortRequest(
        accumulatedFatigue: nil, difficultyRating: nil,
        engagementRating: nil, externalVariablesRating: nil)

    var isEmpty: Bool { self == .unrated }

    /// The rating given on `scale`, or `nil` where it was left alone.
    subscript(scale: EffortScale) -> Int? {
        switch scale {
        case .accumulatedFatigue: accumulatedFatigue
        case .difficulty: difficultyRating
        case .engagement: engagementRating
        case .externalVariables: externalVariablesRating
        }
    }

    /// The scales given a rating, in the order the scales are listed.
    var ratedScales: [EffortScale] {
        EffortScale.allCases.filter { self[$0] != nil }
    }

    /// The ratings with `scale` rated `value`, the others kept. `nil` leaves that scale unrated.
    func rating(_ scale: EffortScale, _ value: Int?) -> PerceivedEffortRequest {
        PerceivedEffortRequest(
            accumulatedFatigue: scale == .accumulatedFatigue ? value : accumulatedFatigue,
            difficultyRating: scale == .difficulty ? value : difficultyRating,
            engagementRating: scale == .engagement ? value : engagementRating,
            externalVariablesRating: scale == .externalVariables ? value : externalVariablesRating)
    }
}

/// The four things the athlete rates a session on.
enum EffortScale: CaseIterable, Sendable {
    case accumulatedFatigue
    case difficulty
    case engagement
    case externalVariables

    /// The values a rating takes.
    ///
    /// One to five, matching `WellnessState`'s four component scores, which is the only other thing
    /// in Lionheart the athlete rates about themselves. The server stores these as plain integers
    /// and validates no range, so the scale is the client's to be consistent about.
    static let values = 1...5
}
