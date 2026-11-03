import Foundation

/// The athlete's own normal for a figure: the mean and spread of the days before the one being read.
///
/// The day itself is excluded, so a bad night cannot drag its own baseline toward it. Four weeks is
/// long enough to be a baseline rather than last week, and short enough to follow a training block.
/// Fewer than a week of readings is not a baseline, and the screen then shows the figure alone.
struct MetricBaseline: Equatable, Sendable {
    let mean: Double
    let standardDeviation: Double
    let sampleCount: Int

    /// The days before the read day that the baseline is drawn from.
    static let windowDays = 28
    static let minimumSamples = 7

    static func of(_ values: [Double]) -> MetricBaseline? {
        guard values.count >= minimumSamples else { return nil }
        let mean = values.reduce(0, +) / Double(values.count)
        let variance = values.reduce(0) { $0 + ($1 - mean) * ($1 - mean) } / Double(values.count - 1)
        return MetricBaseline(mean: mean, standardDeviation: variance.squareRoot(), sampleCount: values.count)
    }

    /// How a value sits against this baseline.
    ///
    /// Within one standard deviation is his normal and says nothing. Outside it, the direction
    /// decides: an HRV well above normal is good news, a lowest heart rate well above normal is not.
    func standing(of value: Double, polarity: RecoveryMetric.Polarity) -> MetricStanding {
        let deviation = value - mean
        guard abs(deviation) > standardDeviation else { return .typical }
        switch polarity {
        case .higherIsBetter: return deviation > 0 ? .favourable : .unfavourable
        case .lowerIsBetter: return deviation < 0 ? .favourable : .unfavourable
        }
    }
}

/// Where a reading sits against the athlete's baseline.
enum MetricStanding: Equatable, Sendable {
    /// Within his normal spread.
    case typical
    case favourable
    case unfavourable
    /// Nothing to compare against: too few days on record, or a figure that is itself a deviation.
    case noBaseline
}
