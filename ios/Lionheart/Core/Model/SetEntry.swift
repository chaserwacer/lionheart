import Foundation

/// One performed set, in the richest shape the log supported.
///
/// The four cases are the server's sealed hierarchy, and looseness is a *smaller* shape rather than
/// a row of nulls: a `.freeform` set keeps the words it was logged with and nothing it does not
/// have. Grouping sets into movements is derivable from consecutive `movementDataId`s, which is why
/// there is no movement wrapper here.
enum SetEntry: Equatable, Sendable {
    case lift(Lift)
    case distanceTime(DistanceTime)
    case trailRun(TrailRun)
    case freeform(Common)
    /// A `$type` this client does not know. It keeps whatever it could read, so an unfamiliar set
    /// shape costs its own detail and not the session.
    case unrecognised(type: String, common: Common)

    /// What every set carries whatever its shape.
    struct Common: Equatable, Sendable {
        let source: Utterance
        let facets: [Facet]
    }

    struct Lift: Equatable, Sendable {
        let common: Common
        let movementDataId: UUID
        let reps: Int
        let weight: Double
        let unit: WeightUnit
        let rpe: Rpe
    }

    struct DistanceTime: Equatable, Sendable {
        let common: Common
        let movementDataId: UUID
        let distance: Double
        let unit: DistanceUnit
        /// Seconds. The wire carries a `TimeSpan`; pace is derived, never stored.
        let duration: TimeInterval
        let rpe: Rpe
    }

    struct TrailRun: Equatable, Sendable {
        let common: Common
        let distance: Double
        let unit: DistanceUnit
        let duration: TimeInterval
        /// Net gain, so a descent-dominant route may legitimately be negative.
        let elevationGainMeters: Double
        let rpe: Rpe
    }

    var common: Common {
        switch self {
        case .lift(let set): set.common
        case .distanceTime(let set): set.common
        case .trailRun(let set): set.common
        case .freeform(let common): common
        case .unrecognised(_, let common): common
        }
    }

    /// The movement this set was performed on, where the shape names one. A trail run and a
    /// freeform set genuinely have no movement identity, which is why this is optional rather
    /// than a sentinel id.
    var movementDataId: UUID? {
        switch self {
        case .lift(let set): set.movementDataId
        case .distanceTime(let set): set.movementDataId
        case .trailRun, .freeform, .unrecognised: nil
        }
    }

    var rpe: Rpe {
        switch self {
        case .lift(let set): set.rpe
        case .distanceTime(let set): set.rpe
        case .trailRun(let set): set.rpe
        case .freeform, .unrecognised: .unrated
        }
    }
}

extension SetEntry: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        let common = Common(
            source: container.lenient(Utterance.self, "source") ?? .formed,
            facets: container.lenientList(Facet.self, "facets"))

        switch type {
        case "lift":
            guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
                self = .unrecognised(type: type, common: common)
                return
            }
            self = .lift(Lift(
                common: common,
                movementDataId: movementDataId,
                reps: container.lenient("reps", or: 0),
                weight: container.lenient("weight", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                rpe: Rpe(wire: container.lenient(Double.self, "rpe"))))

        case "distanceTime":
            guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
                self = .unrecognised(type: type, common: common)
                return
            }
            self = .distanceTime(DistanceTime(
                common: common,
                movementDataId: movementDataId,
                distance: container.lenient("distance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                duration: container.duration("duration") ?? 0,
                rpe: Rpe(wire: container.lenient(Double.self, "rpe"))))

        case "trailRun":
            self = .trailRun(TrailRun(
                common: common,
                distance: container.lenient("distance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                duration: container.duration("duration") ?? 0,
                elevationGainMeters: container.lenient("elevationGainMeters", or: 0),
                rpe: Rpe(wire: container.lenient(Double.self, "rpe"))))

        case "freeform":
            self = .freeform(common)

        default:
            self = .unrecognised(type: type, common: common)
        }
    }
}
