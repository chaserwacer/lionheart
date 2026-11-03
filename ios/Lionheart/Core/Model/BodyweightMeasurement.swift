import Foundation

/// The athlete's bodyweight on one calendar day. One per day: logging the same day again replaces
/// it, so a series of these is a daily series with no duplicates to reconcile.
struct BodyweightMeasurement: Equatable, Sendable, Identifiable {
    let day: CalendarDay
    let weight: Double
    let unit: WeightUnit

    var id: CalendarDay { day }
}

extension BodyweightMeasurement: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day"), let weight = container.lenient(Double.self, "weight")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A bodyweight needs a day and a weight."))
        }
        self.day = day
        self.weight = weight
        unit = WeightUnit(wire: container.lenient(Int.self, "unit"))
    }
}
