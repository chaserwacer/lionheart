import Foundation

/// One of Oura's 0–100 ratings: its readiness, sleep and activity scores, and every contributor
/// behind them.
///
/// **A score, not a measurement.** Each is how Oura rated something against the athlete's own
/// baseline — a resting-heart-rate contributor of 80 is not 80 bpm, and a training-frequency
/// contributor says nothing about how many sessions there were. It is a type of its own so a figure
/// that is Oura's opinion cannot be drawn, compared or averaged as if it were a measured value
/// without the code saying which it is. Screens label it as Oura's and draw it on a 0–100 track.
///
/// The value is a `Double` because resilience's contributors arrive with decimals; the daily
/// scores and the other contributors are whole numbers.
struct OuraScore: Equatable, Comparable, Hashable, Sendable {
    let value: Double

    init(_ value: Double) { self.value = value }

    init(_ value: Int) { self.value = Double(value) }

    /// The scale every one of Oura's scores lives on.
    static let scale: ClosedRange<Double> = 0...100

    /// The score as Oura shows it, a whole number.
    var rounded: Int { Int(value.rounded()) }

    static func < (lhs: Self, rhs: Self) -> Bool { lhs.value < rhs.value }
}

extension OuraScore: CustomStringConvertible {
    var description: String { "\(rounded)" }
}

extension OuraScore: Decodable {
    init(from decoder: any Decoder) throws {
        self.init(try decoder.singleValueContainer().decode(Double.self))
    }
}
