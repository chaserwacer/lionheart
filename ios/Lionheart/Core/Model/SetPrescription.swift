import Foundation

/// A target set on a planned session — intentions only.
///
/// Three cases, not four: the server has no trail-run prescription, because a planned trail run is
/// prescribed as a distance and a time like any other. Actuals live exclusively on `SetEntry`, so
/// planned and performed never half-fill one another.
enum SetPrescription: Equatable, Sendable {
    case lift(Lift)
    case distanceTime(DistanceTime)
    case freeform(text: String)
    case unrecognised(String)

    struct Lift: Equatable, Sendable {
        let movementDataId: UUID
        let targetReps: Int
        let targetWeight: Double
        let unit: WeightUnit
        let targetRpe: Rpe
    }

    struct DistanceTime: Equatable, Sendable {
        let movementDataId: UUID
        let targetDistance: Double
        let unit: DistanceUnit
        /// Seconds.
        let targetDuration: TimeInterval
    }
}

extension SetPrescription: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "lift":
            guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
                self = .unrecognised(type)
                return
            }
            self = .lift(Lift(
                movementDataId: movementDataId,
                targetReps: container.lenient("targetReps", or: 0),
                targetWeight: container.lenient("targetWeight", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                targetRpe: Rpe(wire: container.lenient(Double.self, "targetRpe"))))

        case "distanceTime":
            guard let movementDataId = container.lenient(UUID.self, "movementDataId") else {
                self = .unrecognised(type)
                return
            }
            self = .distanceTime(DistanceTime(
                movementDataId: movementDataId,
                targetDistance: container.lenient("targetDistance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                targetDuration: container.duration("targetDuration") ?? 0))

        case "freeform":
            self = .freeform(text: container.lenient("text", or: ""))

        default:
            self = .unrecognised(type)
        }
    }
}
