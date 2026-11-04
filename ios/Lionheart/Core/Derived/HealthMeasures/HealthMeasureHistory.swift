import Foundation

/// Every Apple Health figure the server keeps, as the screens read them.
struct HealthMeasureHistory: Equatable, Sendable {
    let series: [HealthMeasureSeries]

    /// The days of a figure as one device measured it, oldest first; empty where it measured none.
    func days(_ metric: HealthMetric, by device: MeasuringDevice) -> [MeasuredDay] {
        series.first { $0.metric == metric && $0.device == device }?.days ?? []
    }

    /// The device that measured a figure on the most days, with its days — the one series a screen
    /// draws for it. One device, never a blend: the watch and the phone sit at different places on
    /// the body and read gait differently.
    func principal(_ metric: HealthMetric) -> (device: MeasuringDevice, days: [MeasuredDay])? {
        series
            .filter { $0.metric == metric && !$0.days.isEmpty }
            .max { $0.days.count < $1.days.count }
            .map { ($0.device, $0.days) }
    }

    var isEmpty: Bool { series.allSatisfy(\.days.isEmpty) }
}
