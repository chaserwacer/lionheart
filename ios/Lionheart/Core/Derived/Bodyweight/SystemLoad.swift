import Foundation

/// The load a lift actually moved where the logged weight is plate added to the athlete's own body:
/// bodyweight plus the plate.
///
/// Only for a movement whose setup says its weight is added to bodyweight, and only with a
/// bodyweight in the lift's own unit — weights never convert, so a pounds dip with a kilogram
/// bodyweight has no honest system load and gets none.
struct SystemLoad: Equatable, Sendable {
    let total: Double
    let unit: WeightUnit
    /// The bodyweight it was built on, so a screen can say which day's reading that was.
    let bodyweight: BodyweightMeasurement

    static func of(
        weight: Double, unit: WeightUnit, setup: MovementSetup, bodyweight: BodyweightMeasurement?
    ) -> SystemLoad? {
        guard setup.addsToBodyweight, let bodyweight, bodyweight.unit == unit else { return nil }
        return SystemLoad(total: bodyweight.weight + weight, unit: unit, bodyweight: bodyweight)
    }
}
