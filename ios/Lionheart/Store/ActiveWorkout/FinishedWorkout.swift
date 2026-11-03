import Foundation

/// A workout the athlete has finished that the server has not taken yet.
///
/// Finishing is the athlete's act and it happens where they are, which is often where there is no
/// signal. So a finish that cannot reach the server is not refused: the workout is set down here,
/// with the end and the effort the athlete gave it, and delivered when the server answers. Until
/// then this is the only copy, which is why it is a document on disk and not a request in memory.
struct FinishedWorkout: Equatable, Sendable, Identifiable {
    let id: UUID
    let workout: ActiveWorkout
    let endedAt: Date
    let effort: PerceivedEffortRequest?
    let delivery: Delivery

    /// Where handing it over stands.
    enum Delivery: Equatable, Sendable {
        /// Not delivered yet. The next time the server answers, it is tried again.
        case waiting
        /// The server will not take it, for the reason given. Trying again cannot help, so it waits
        /// for the athlete instead.
        case refused(String)
    }

    init(
        id: UUID = UUID(),
        workout: ActiveWorkout,
        endedAt: Date,
        effort: PerceivedEffortRequest?,
        delivery: Delivery = .waiting
    ) {
        self.id = id
        self.workout = workout
        self.endedAt = endedAt
        self.effort = effort
        self.delivery = delivery
    }

    func refused(_ reason: String) -> FinishedWorkout {
        FinishedWorkout(id: id, workout: workout, endedAt: endedAt, effort: effort, delivery: .refused(reason))
    }

    /// Whether delivering it again could plausibly land it.
    var isWorthDelivering: Bool {
        switch delivery {
        case .waiting: true
        case .refused: false
        }
    }
}

extension FinishedWorkout: Codable {
    private enum CodingKeys: String, CodingKey {
        case id, workout, endedAt, effort, delivery
    }

    /// The workout itself is decoded strictly, since it is the athlete's work; everything about the
    /// delivery is read leniently, and an unreadable delivery reads as waiting, which only costs a
    /// request.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(UUID.self, forKey: .id)
        workout = try container.decode(ActiveWorkout.self, forKey: .workout)
        endedAt = try container.decode(Date.self, forKey: .endedAt)
        effort = try? container.decodeIfPresent(PerceivedEffortRequest.self, forKey: .effort)
        delivery = (try? container.decode(Delivery.self, forKey: .delivery)) ?? .waiting
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(workout, forKey: .workout)
        try container.encode(endedAt, forKey: .endedAt)
        try container.encodeIfPresent(effort, forKey: .effort)
        try container.encode(delivery, forKey: .delivery)
    }
}

extension FinishedWorkout.Delivery: Codable {
    private enum CodingKeys: String, CodingKey {
        case kind, reason
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        switch try container.decode(String.self, forKey: .kind) {
        case "refused":
            self = .refused(try container.decodeIfPresent(String.self, forKey: .reason) ?? "")
        // "waiting", or a state a later build invents: trying again is the reading that loses
        // nothing.
        default:
            self = .waiting
        }
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .waiting:
            try container.encode("waiting", forKey: .kind)
        case .refused(let reason):
            try container.encode("refused", forKey: .kind)
            try container.encode(reason, forKey: .reason)
        }
    }
}
