import Foundation

/// A record's kind fused with the performance holding it, as the server's `AchievementView`
/// sends it.
///
/// Seven cases, each carrying only its own measurement, so a heaviest-set record cannot hold a
/// distance. The derived figures — an estimated one-rep max, a volume, a pace — are **received,
/// never computed here**: the server derives them once per response from the stored truth, and a
/// second derivation on the device is how two numbers for one record start to disagree.
enum Achievement: Equatable, Sendable {
    case heaviestSet(Lift)
    case bestSetVolume(SetVolume)
    /// The heaviest weight held for `reps` reps, by a set of exactly that many or the front of a
    /// longer one — a set of seven contains a set of six. The `Lift` is the set as performed, so
    /// its reps are at least `reps`, and more when the record was carried down from it.
    case repMax(reps: Int, Lift)
    case bestSessionVolume(SessionVolume)
    case longestDistance(Distance)
    case fastestTime(meters: Int, Distance)
    case bestPace(Pace)
    /// A `$type` this build does not know. The record still renders, as a record it cannot show.
    case unrecognised(String)

    /// One set as performed, as the two single-set weight kinds hold it.
    struct Lift: Equatable, Sendable {
        let weight: Double
        let unit: WeightUnit
        let reps: Int
        let estimatedOneRepMax: Double
    }

    struct SetVolume: Equatable, Sendable {
        let volume: Double
        let unit: WeightUnit
        let weight: Double
        let reps: Int
    }

    struct SessionVolume: Equatable, Sendable {
        let volume: Double
        let unit: WeightUnit
    }

    /// One effort, in the unit it was logged in.
    struct Distance: Equatable, Sendable {
        let distance: Double
        let unit: DistanceUnit
        /// Seconds. The wire carries a `TimeSpan`.
        let duration: TimeInterval
    }

    struct Pace: Equatable, Sendable {
        /// Seconds per kilometer and per mile, both the server's. The screens read per mile.
        let perKilometer: TimeInterval
        let perMile: TimeInterval
        let effort: Distance
    }

    var kind: RecordKind {
        switch self {
        case .heaviestSet: .heaviestSet
        case .bestSetVolume: .bestSetVolume
        case .repMax(let reps, _): .repMax(reps: reps)
        case .bestSessionVolume: .bestSessionVolume
        case .longestDistance: .longestDistance
        case .fastestTime(let meters, _): .fastestTime(meters: meters)
        case .bestPace: .bestPace
        case .unrecognised(let wire): .unrecognised(wire)
        }
    }

    /// The weight unit this is held in, which is part of the record's identity — see `RecordKey`.
    /// Absent for the distance kinds, which rank in meters.
    var heldIn: WeightUnit? {
        switch self {
        case .heaviestSet(let lift), .repMax(_, let lift): lift.unit
        case .bestSetVolume(let set): set.unit
        case .bestSessionVolume(let session): session.unit
        case .longestDistance, .fastestTime, .bestPace, .unrecognised: nil
        }
    }

    /// The set holding a heaviest set or a rep max, as performed.
    var performedSet: Lift? {
        switch self {
        case .heaviestSet(let lift), .repMax(_, let lift): lift
        case .bestSetVolume, .bestSessionVolume, .longestDistance, .fastestTime, .bestPace,
             .unrecognised:
            nil
        }
    }

    /// The server's estimate of a single rep, for the kinds that carry one. It is the performed
    /// set's, so a rep max carried down from a longer set estimates from the whole set.
    var estimatedOneRepMax: Double? { performedSet?.estimatedOneRepMax }
}

extension Achievement: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        func lift(reps: Int) -> Lift {
            Lift(
                weight: container.lenient("weight", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                reps: reps,
                estimatedOneRepMax: container.lenient("estimatedOneRepMax", or: 0))
        }

        func distance() -> Distance {
            Distance(
                distance: container.lenient("distance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                duration: container.duration("duration") ?? 0)
        }

        switch type {
        case "heaviestSet":
            self = .heaviestSet(lift(reps: container.lenient("reps", or: 0)))
        case "repMax":
            // `reps` is the rep count the record is held at and `setReps` the set's own. A body
            // without `setReps` comes from a server whose rep maxes were all held by a set of
            // exactly their count.
            let reps: Int = container.lenient("reps", or: 0)
            self = .repMax(reps: reps, lift(reps: container.lenient("setReps", or: reps)))
        case "bestSetVolume":
            self = .bestSetVolume(SetVolume(
                volume: container.lenient("volume", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                weight: container.lenient("weight", or: 0),
                reps: container.lenient("reps", or: 0)))
        case "bestSessionVolume":
            self = .bestSessionVolume(SessionVolume(
                volume: container.lenient("volume", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit"))))
        case "longestDistance":
            self = .longestDistance(distance())
        case "fastestTime":
            self = .fastestTime(meters: container.lenient("distanceMeters", or: 0), distance())
        case "bestPace":
            self = .bestPace(Pace(
                perKilometer: container.duration("pacePerKilometer") ?? 0,
                perMile: container.duration("pacePerMile") ?? 0,
                effort: distance()))
        default:
            self = .unrecognised(type)
        }
    }
}
