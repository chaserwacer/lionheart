import Foundation

/// Rate of perceived exertion for a set: rated with a value in [0, 10], or not rated.
///
/// Two cases rather than an optional `Double`, because the UI renders them differently — an unrated
/// set shows no RPE at all, it does not show zero. The server models the same distinction
/// (`Rpe.Unrated`), and on the wire the value is a bare number or `null`.
enum Rpe: Equatable, Sendable {
    case unrated
    case rated(Double)

    /// Clamped rather than refused: the server already guarantees [0, 10], so a value outside it
    /// is a corrupt row, and the nearest legal reading of it is more use than discarding the set.
    init(wire: Double?) {
        guard let wire, wire.isFinite else { self = .unrated; return }
        self = .rated(min(max(wire, 0), 10))
    }

    var value: Double? {
        switch self {
        case .unrated: nil
        case .rated(let value): value
        }
    }
}

/// How a set's weight was measured. Integer-valued on the wire, and the order is the server's.
///
/// `Hashable` for two reasons that arrived together: a `Picker` tags with it, because the unit is a
/// choice the athlete makes on the entry row; and a trend series is keyed by the movement *and* its
/// unit, so an athlete who switched from pounds to kilograms gets two series rather than one line
/// with a cliff in it.
enum WeightUnit: Hashable, Sendable {
    case kilograms
    case pounds
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .kilograms
        case 1: self = .pounds
        default: self = .unrecognised(wire ?? -1)
        }
    }

    var abbreviation: String {
        switch self {
        case .kilograms: "kg"
        case .pounds: "lb"
        case .unrecognised: ""
        }
    }

    /// The integer the server reads. An unrecognised unit sends back the number it arrived as, so
    /// a set this build could not name is still re-sent as what it was rather than silently
    /// becoming kilograms.
    var wire: Int {
        switch self {
        case .kilograms: 0
        case .pounds: 1
        case .unrecognised(let value): value
        }
    }
}

/// How a set's distance was measured. Integer-valued on the wire, and the order is the server's.
enum DistanceUnit: Hashable, Sendable {
    case meters
    case yards
    case miles
    case kilometers
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .meters
        case 1: self = .yards
        case 2: self = .miles
        case 3: self = .kilometers
        default: self = .unrecognised(wire ?? -1)
        }
    }

    var abbreviation: String {
        switch self {
        case .meters: "m"
        case .yards: "yd"
        case .miles: "mi"
        case .kilometers: "km"
        case .unrecognised: ""
        }
    }

    /// The integer the server reads. See `WeightUnit.wire` for why an unrecognised unit keeps its
    /// number.
    var wire: Int {
        switch self {
        case .meters: 0
        case .yards: 1
        case .miles: 2
        case .kilometers: 3
        case .unrecognised(let value): value
        }
    }
}
