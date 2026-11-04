import Foundation

/// What an endurance set was: a ride, a hike, a walk, or a ride or run indoors.
///
/// Indoor is part of the activity because it changes what the session is evidence of — a trainer's
/// or a treadmill's distance is the machine's estimate, so an indoor ride or run counts as training
/// and never as a record. An outdoor run is not here: it is a trail run.
enum EnduranceActivity: Hashable, Sendable {
    case ride
    case indoorRide
    case hike
    case walk
    case indoorRun
    case unrecognised(String)

    init(wire: String) {
        switch wire {
        case "ride": self = .ride
        case "indoorRide": self = .indoorRide
        case "hike": self = .hike
        case "walk": self = .walk
        case "indoorRun": self = .indoorRun
        default: self = .unrecognised(wire)
        }
    }

    var wire: String {
        switch self {
        case .ride: "ride"
        case .indoorRide: "indoorRide"
        case .hike: "hike"
        case .walk: "walk"
        case .indoorRun: "indoorRun"
        case .unrecognised(let wire): wire
        }
    }

    /// The activity as a title: "Indoor ride".
    var title: String {
        switch self {
        case .ride: "Ride"
        case .indoorRide: "Indoor ride"
        case .hike: "Hike"
        case .walk: "Walk"
        case .indoorRun: "Indoor run"
        case .unrecognised: "Endurance"
        }
    }

    /// Whether it is measured as a speed rather than a pace: a ride, indoors or out.
    var readsAsSpeed: Bool {
        switch self {
        case .ride, .indoorRide: true
        case .hike, .walk, .indoorRun, .unrecognised: false
        }
    }
}
