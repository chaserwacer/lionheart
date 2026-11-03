import Foundation

/// Which question a record answers, bracket included — the server's `RecordKind`.
///
/// Two kinds are bracketed: a five-rep max and a three-rep max are different records on one
/// movement, and a fastest 5 km is a different record from a fastest 10 km. The bracket is part of
/// the case so a rep max without a rep count cannot be written down.
enum RecordKind: Hashable, Sendable {
    case heaviestSet
    case bestSetVolume
    case repMax(reps: Int)
    case bestSessionVolume
    case longestDistance
    /// The quickest cover of exactly this many meters — the distance actually run, rounded to the
    /// meter, not a standard race distance.
    case fastestTime(meters: Int)
    case bestPace
    case unrecognised(String)

    /// The highest rep count the server keeps a rep max for — `RecordKind.HighestGradedRepCount`.
    /// Past it the number describes work capacity, which the volume records answer.
    static let highestGradedRepCount = 12

    /// The spelling the history route's `kind` parameter takes, which is also the achievement's
    /// `$type`.
    var wireName: String {
        switch self {
        case .heaviestSet: "heaviestSet"
        case .bestSetVolume: "bestSetVolume"
        case .repMax: "repMax"
        case .bestSessionVolume: "bestSessionVolume"
        case .longestDistance: "longestDistance"
        case .fastestTime: "fastestTime"
        case .bestPace: "bestPace"
        case .unrecognised(let wire): wire
        }
    }

    /// The history route's `bracket`, which it requires for a bracketed kind and refuses for any
    /// other — so this is absent rather than zero.
    var bracket: Int? {
        switch self {
        case .repMax(let reps): reps
        case .fastestTime(let meters): meters
        case .heaviestSet, .bestSetVolume, .bestSessionVolume, .longestDistance, .bestPace,
             .unrecognised:
            nil
        }
    }

    /// Whether a history of this kind can be asked for at all. An unrecognised kind has no
    /// bracket this build knows how to state, so asking would be a guess the server answers 400.
    var hasHistory: Bool {
        switch self {
        case .heaviestSet, .bestSetVolume, .repMax, .bestSessionVolume, .longestDistance,
             .fastestTime, .bestPace:
            true
        case .unrecognised:
            false
        }
    }

    var label: String {
        switch self {
        case .heaviestSet: "Heaviest set"
        case .bestSetVolume: "Best set volume"
        case .repMax(let reps): "\(reps)-rep max"
        case .bestSessionVolume: "Best session volume"
        case .longestDistance: "Longest distance"
        case .fastestTime: "Fastest time"
        case .bestPace: "Best pace"
        case .unrecognised: "Record"
        }
    }
}

/// What makes two records the same record: the subject, the kind, and the weight unit for the
/// kinds measured in one — the server's `RecordKey`.
///
/// **The unit is part of the identity.** Weights never convert, so a movement logged in both units
/// holds a kilograms record and a pounds record of the same kind, each set and beaten within its
/// own. Anything that groups, looks up or filters records keys on this, never on the kind alone —
/// keyed on the kind, the second unit's record would silently overwrite the first. Distances carry
/// no unit here: they rank in meters, so one record covers every spelling of a distance.
struct RecordKey: Hashable, Sendable {
    let subject: RecordSubject
    let kind: RecordKind
    let heldIn: WeightUnit?
}
