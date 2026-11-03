import Foundation

/// The plates on a barbell, and what loading one to a weight takes.
///
/// Pounds only, on a 45 lb bar with the plates a commercial gym racks. The athlete logs in pounds,
/// and a kilogram set gets no breakdown rather than one made up for a gym this app has never seen.
struct PlateSet: Equatable, Sendable {
    let bar: Double
    /// The plate sizes, heaviest first. A side takes as many of each as fit.
    let plates: [Double]
    let unit: WeightUnit

    static let pounds = PlateSet(bar: 45, plates: [45, 35, 25, 10, 5, 2.5], unit: .pounds)

    /// The smallest step a pair of the lightest plates makes.
    var increment: Double { (plates.min() ?? 0) * 2 }

    /// The plate set for a unit, where there is one.
    static func `for`(_ unit: WeightUnit) -> PlateSet? {
        switch unit {
        case .pounds: .pounds
        case .kilograms, .unrecognised: nil
        }
    }
}

/// What goes on each side of the bar for one total weight.
enum PlateLoading: Equatable, Sendable {
    /// The bar alone, or less: nothing to load.
    case barOnly
    /// The plates per side, heaviest first, and whatever the plates cannot make — a total that is not
    /// a multiple of the smallest pair is loaded as near under it as the plates allow.
    case loaded(perSide: [Double], short: Double)

    static func of(_ total: Double, on set: PlateSet = .pounds) -> PlateLoading {
        let perSide = (total - set.bar) / 2
        guard perSide > 0 else { return .barOnly }

        let (plates, left) = set.plates.reduce(into: ([Double](), perSide)) { state, plate in
            // A small tolerance, so 2.5 is not lost to a floating-point remainder of 2.4999…
            while state.1 + 1e-9 >= plate {
                state.0.append(plate)
                state.1 -= plate
            }
        }
        let short = max(0, left * 2)
        return .loaded(perSide: plates, short: short < 1e-6 ? 0 : short)
    }

    /// The total the plates actually make, which is the weight to log.
    func total(on set: PlateSet = .pounds) -> Double {
        switch self {
        case .barOnly: set.bar
        case .loaded(let perSide, _): set.bar + 2 * perSide.reduce(0, +)
        }
    }
}
