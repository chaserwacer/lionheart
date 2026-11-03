import Foundation

/// A performed set as the athlete states it: the numbers, and nothing the server owns.
///
/// Deliberately **not** `SetEntry`. A set the server records carries `source` and `facets`, which
/// only the server and the interpretation engine may assign — so the shape a client sends is the
/// smaller one, and the two types are kept apart rather than one made to serve both. It mirrors the
/// backend's `SetCommand` hierarchy field for field.
///
/// It is also what the active workout persists, which is the second reason it exists: the document
/// on disk then holds exactly what the next `PUT /{id}/sets` will carry, so there is one shape to
/// reason about between the gym floor and the server.
///
/// There is no `.unrecognised` case, which is the opposite of every response type in this folder
/// and is correct for the opposite reason: nothing but this client ever writes one. A `$type` this
/// build does not know can only come from a document a *newer* build wrote, and
/// `ActiveWorkout.isReadable` refuses those whole rather than half-reading them — so here a strange
/// discriminator means a corrupt document, and throwing is what makes it read as absent.
enum SetCommand: Equatable, Sendable {
    case lift(Lift)
    case distanceTime(DistanceTime)
    case trailRun(TrailRun)
    /// A set the athlete described instead of measuring. The words are the record.
    case freeform(text: String)

    struct Lift: Equatable, Sendable {
        let movementDataId: UUID
        let reps: Int
        let weight: Double
        let unit: WeightUnit
        let rpe: Rpe
        /// Working unless the athlete said otherwise, which is what nearly every set is.
        let kind: SetKind

        init(
            movementDataId: UUID, reps: Int, weight: Double, unit: WeightUnit, rpe: Rpe,
            kind: SetKind = .working
        ) {
            self.movementDataId = movementDataId
            self.reps = reps
            self.weight = weight
            self.unit = unit
            self.rpe = rpe
            self.kind = kind
        }

        /// The same lift as another kind of set.
        func marked(_ kind: SetKind) -> Lift {
            Lift(movementDataId: movementDataId, reps: reps, weight: weight, unit: unit, rpe: rpe, kind: kind)
        }
    }

    struct DistanceTime: Equatable, Sendable {
        let movementDataId: UUID
        let distance: Double
        let unit: DistanceUnit
        /// Seconds. The wire carries a `TimeSpan`.
        let duration: TimeInterval
        let rpe: Rpe
    }

    struct TrailRun: Equatable, Sendable {
        let distance: Double
        let unit: DistanceUnit
        /// Seconds.
        let duration: TimeInterval
        /// Net gain, so a descent-dominant route may legitimately be negative.
        let elevationGainMeters: Double
        let rpe: Rpe
    }

    /// The movement this set was performed on, where the shape names one. A trail run and a
    /// freeform set genuinely have no movement identity.
    var movementDataId: UUID? {
        switch self {
        case .lift(let set): set.movementDataId
        case .distanceTime(let set): set.movementDataId
        case .trailRun, .freeform: nil
        }
    }

    var rpe: Rpe {
        switch self {
        case .lift(let set): set.rpe
        case .distanceTime(let set): set.rpe
        case .trailRun(let set): set.rpe
        case .freeform: .unrated
        }
    }

    /// What the set was for. Only a lift has a kind; everything else is the work itself.
    var kind: SetKind {
        switch self {
        case .lift(let set): set.kind
        case .distanceTime, .trailRun, .freeform: .working
        }
    }

    /// Whether this set counts as work — toward totals, targets and records. A warm-up does not.
    var isWork: Bool { kind.isWork }

    /// Which entry shape this set is, so a screen can offer the same one again without
    /// reconstructing the set. `.trailRun` and `.freeform` are shapes a movement never carries.
    var modality: SetModality {
        switch self {
        case .lift: .lift
        case .distanceTime: .distanceTime
        case .trailRun: .trailRun
        case .freeform: .freeform
        }
    }
}

/// Which of the four shapes a set is being logged in.
///
/// Its own type because the choice outlives any one set: the entry surface offers the shape the
/// athlete last used on a movement, and a movement's identity (`MovementData`) does not say whether
/// it is lifted or run — the same treadmill could be either.
enum SetModality: String, Equatable, Sendable, CaseIterable {
    case lift
    case distanceTime
    case trailRun
    case freeform
}

// MARK: - Wire

extension SetCommand: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        switch type {
        case "lift":
            self = .lift(Lift(
                movementDataId: try container.required(UUID.self, "movementDataId", type),
                reps: container.lenient("reps", or: 0),
                weight: container.lenient("weight", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                rpe: Rpe(wire: container.lenient(Double.self, "rpe")),
                // A document written before sets had a kind holds working sets.
                kind: SetKind(wire: container.lenient(Int.self, "kind"))))

        case "distanceTime":
            self = .distanceTime(DistanceTime(
                movementDataId: try container.required(UUID.self, "movementDataId", type),
                distance: container.lenient("distance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                duration: container.duration("duration") ?? 0,
                rpe: Rpe(wire: container.lenient(Double.self, "rpe"))))

        case "trailRun":
            self = .trailRun(TrailRun(
                distance: container.lenient("distance", or: 0),
                unit: DistanceUnit(wire: container.lenient(Int.self, "unit")),
                duration: container.duration("duration") ?? 0,
                elevationGainMeters: container.lenient("elevationGainMeters", or: 0),
                rpe: Rpe(wire: container.lenient(Double.self, "rpe"))))

        case "freeform":
            self = .freeform(text: container.lenient("text", or: ""))

        default:
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "'\(type)' is not a set shape this build can write back."))
        }
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)

        switch self {
        case .lift(let set):
            try container.encode("lift", forKey: .type)
            try container.encode(set.movementDataId, forKey: WireKey(stringValue: "movementDataId"))
            try container.encode(set.reps, forKey: WireKey(stringValue: "reps"))
            try container.encode(set.weight, forKey: WireKey(stringValue: "weight"))
            try container.encode(set.unit.wire, forKey: WireKey(stringValue: "unit"))
            try container.encode(set.rpe.value, forKey: WireKey(stringValue: "rpe"))
            try container.encode(set.kind.wire, forKey: WireKey(stringValue: "kind"))

        case .distanceTime(let set):
            try container.encode("distanceTime", forKey: .type)
            try container.encode(set.movementDataId, forKey: WireKey(stringValue: "movementDataId"))
            try container.encode(set.distance, forKey: WireKey(stringValue: "distance"))
            try container.encode(set.unit.wire, forKey: WireKey(stringValue: "unit"))
            try container.encode(
                WireDuration.wire(fromSeconds: set.duration), forKey: WireKey(stringValue: "duration"))
            try container.encode(set.rpe.value, forKey: WireKey(stringValue: "rpe"))

        case .trailRun(let set):
            try container.encode("trailRun", forKey: .type)
            try container.encode(set.distance, forKey: WireKey(stringValue: "distance"))
            try container.encode(set.unit.wire, forKey: WireKey(stringValue: "unit"))
            try container.encode(
                WireDuration.wire(fromSeconds: set.duration), forKey: WireKey(stringValue: "duration"))
            try container.encode(
                set.elevationGainMeters, forKey: WireKey(stringValue: "elevationGainMeters"))
            try container.encode(set.rpe.value, forKey: WireKey(stringValue: "rpe"))

        case .freeform(let text):
            try container.encode("freeform", forKey: .type)
            try container.encode(text, forKey: WireKey(stringValue: "text"))
        }
    }
}

extension KeyedDecodingContainer where Key == WireKey {
    /// A value the shape cannot do without, as a throw naming the shape that wanted it.
    ///
    /// The lenient reads elsewhere are right for a response, where a missing member should cost
    /// that member. A set the device wrote and cannot read back has lost the athlete's work, and
    /// pretending it was a lift of nothing would hide that.
    func required<T: Decodable>(_ type: T.Type, _ key: String, _ shape: String) throws -> T {
        guard let value = lenient(T.self, key) else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: codingPath,
                debugDescription: "A '\(shape)' set needs '\(key)'."))
        }
        return value
    }
}

extension SetEntry {
    /// The set as the athlete would state it, where it has a shape a client can state: how a past
    /// session's sets are read with the arithmetic the workout in flight uses, and how a session
    /// held on the server is taken over as a workout.
    var command: SetCommand? {
        switch self {
        case .lift(let set):
            .lift(SetCommand.Lift(
                movementDataId: set.movementDataId, reps: set.reps, weight: set.weight,
                unit: set.unit, rpe: set.rpe, kind: set.kind))
        case .distanceTime(let set):
            .distanceTime(SetCommand.DistanceTime(
                movementDataId: set.movementDataId, distance: set.distance, unit: set.unit,
                duration: set.duration, rpe: set.rpe))
        case .trailRun(let set):
            .trailRun(SetCommand.TrailRun(
                distance: set.distance, unit: set.unit, duration: set.duration,
                elevationGainMeters: set.elevationGainMeters, rpe: set.rpe))
        case .freeform(let common):
            .freeform(text: common.source.rawText ?? "")
        case .unrecognised:
            nil
        }
    }
}
