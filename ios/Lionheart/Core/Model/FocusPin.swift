import Foundation

/// A movement pinned for emphasis over a horizon — the server's `FocusPinView`.
///
/// The server stores two things about a pin's life: whether it is still pinned, and its terms.
/// Whether its horizon has begun or passed, and whether its target has been met, are readings made
/// here against today and the log (`FocusReading`), the way a planned session is read as missed.
struct FocusPin: Equatable, Sendable, Identifiable {
    let focusPinId: UUID
    let movementDataId: UUID
    let horizon: FocusHorizon
    /// The sessions a week the pin asks for, one to seven.
    let sessionsPerWeek: Int
    let target: FocusTarget
    let status: FocusStatus

    var id: UUID { focusPinId }

    var isPinned: Bool {
        switch status {
        case .pinned: true
        case .unpinned, .unrecognised: false
        }
    }
}

/// The days a pin covers, both ends included.
struct FocusHorizon: Equatable, Sendable {
    let starts: CalendarDay
    let ends: CalendarDay

    /// Whole days in the horizon, both ends counted.
    func dayCount(in calendar: Calendar = .current) -> Int {
        starts.days(until: ends, in: calendar) + 1
    }
}

/// What a pin is working toward: nothing in particular, or one set.
enum FocusTarget: Equatable, Sendable {
    case none
    /// A load for a number of reps — `+70 lb × 5` on a weighted dip, where the load is what the
    /// athlete records for the movement.
    case lift(weight: Double, unit: WeightUnit, reps: Int)
    /// A `$type` this build does not know. The pin still shows; its target does not.
    case unrecognised(String)
}

/// Whether a pin is still pinned. Two stored states; everything else about a pin's progress is read.
enum FocusStatus: Equatable, Sendable {
    case pinned
    /// Taken off the list on a day the athlete chose, before or after its horizon ran out. Kept,
    /// because what was emphasised and for how long is part of the record.
    case unpinned(on: CalendarDay)
    case unrecognised(String)
}

extension FocusPin: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let focusPinId = container.lenient(UUID.self, "focusPinId"),
              let movementDataId = container.lenient(UUID.self, "movementDataId"),
              let starts = container.day("starts"),
              let ends = container.day("ends")
        else {
            // A pin with no movement or no horizon has nothing to read progress against, so it is
            // the one row the list drops.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A focus pin needs its id, its movement and its horizon."))
        }

        self.init(
            focusPinId: focusPinId,
            movementDataId: movementDataId,
            horizon: FocusHorizon(starts: starts, ends: ends),
            sessionsPerWeek: container.lenient("sessionsPerWeek", or: 1),
            target: container.lenient(FocusTarget.self, "target") ?? .none,
            status: container.lenient(FocusStatus.self, "status") ?? .unrecognised(""))
    }
}

extension FocusTarget: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "none":
            self = .none
        case "lift":
            self = .lift(
                weight: container.lenient("weight", or: 0),
                unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
                reps: container.lenient("reps", or: 0))
        default:
            self = .unrecognised(type)
        }
    }
}

extension FocusStatus: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "pinned":
            self = .pinned
        case "unpinned":
            guard let on = container.day("on") else {
                self = .unrecognised(type)
                return
            }
            self = .unpinned(on: on)
        default:
            self = .unrecognised(type)
        }
    }
}
