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

extension SetPrescription: Encodable {
    /// The same shape it decodes from, because the server's `SetPrescriptionCommand` mirrors
    /// `SetPrescription` field for field — nothing on a prescription is server-owned.
    ///
    /// It exists so the active workout can carry the prescriptions of the session it was started
    /// from in its own document: the targets have to survive a force-quit in a basement, and
    /// re-reading them would need the signal that is missing exactly when it matters.
    ///
    /// An `.unrecognised` prescription encodes as its bare discriminator. Nothing was kept from it
    /// to write back — so it round-trips through the document as still-unrecognised, which is the
    /// truth about it, and it is why this type is persisted rather than sent.
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)

        switch self {
        case .lift(let prescription):
            try container.encode("lift", forKey: .type)
            try container.encode(
                prescription.movementDataId, forKey: WireKey(stringValue: "movementDataId"))
            try container.encode(prescription.targetReps, forKey: WireKey(stringValue: "targetReps"))
            try container.encode(
                prescription.targetWeight, forKey: WireKey(stringValue: "targetWeight"))
            try container.encode(prescription.unit.wire, forKey: WireKey(stringValue: "unit"))
            try container.encode(
                prescription.targetRpe.value, forKey: WireKey(stringValue: "targetRpe"))

        case .distanceTime(let prescription):
            try container.encode("distanceTime", forKey: .type)
            try container.encode(
                prescription.movementDataId, forKey: WireKey(stringValue: "movementDataId"))
            try container.encode(
                prescription.targetDistance, forKey: WireKey(stringValue: "targetDistance"))
            try container.encode(prescription.unit.wire, forKey: WireKey(stringValue: "unit"))
            try container.encode(
                WireDuration.wire(fromSeconds: prescription.targetDuration),
                forKey: WireKey(stringValue: "targetDuration"))

        case .freeform(let text):
            try container.encode("freeform", forKey: .type)
            try container.encode(text, forKey: WireKey(stringValue: "text"))

        case .unrecognised(let type):
            try container.encode(type, forKey: .type)
        }
    }
}
