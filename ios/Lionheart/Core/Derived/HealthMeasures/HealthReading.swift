import Foundation

/// One reading of a figure as Health holds it, lifted out of HealthKit's types by `Health/` so the
/// rules below are plain values and tested without a Health store.
struct HealthReading: Equatable, Sendable {
    let metric: HealthMetric
    let at: Date
    /// In the figure's own unit (`HealthMetric`).
    let value: Double
    let source: ReadingSource
}

/// Who wrote a reading to Health, as its source revision says.
struct ReadingSource: Equatable, Sendable {
    /// `com.apple.health.<device id>` for a reading one of Apple's devices measured itself.
    let bundleIdentifier: String
    /// The hardware the writing app ran on — "Watch6,2", "iPhone14,5" — where Health recorded it.
    let productType: String?
}

extension MeasuringDevice {
    /// The Apple device that measured a reading, or `nil` for one another app wrote.
    ///
    /// Both halves are needed. The bundle prefix is Health's mark on what Apple's own sensors
    /// measured; without it, Oura's app — which writes its RMSSD under Health's SDNN type — would read
    /// as the iPhone it runs on. The product type then says which device: the watch writes from the
    /// watch and the phone's gait from the phone. A reading typed into the Health app by hand comes
    /// from `com.apple.Health`, without the device suffix, and is left out with the apps'.
    static func measuring(_ source: ReadingSource) -> MeasuringDevice? {
        guard source.bundleIdentifier.lowercased().hasPrefix("com.apple.health."), let product = source.productType
        else { return nil }
        if product.hasPrefix("Watch") { return .appleWatch }
        if product.hasPrefix("iPhone") { return .iPhone }
        return nil
    }
}

/// Health's readings reduced to the days the server keeps.
enum DailyMeasures {
    /// Health's readings as days, and how many another app wrote.
    struct Reduced: Equatable, Sendable {
        /// Oldest first.
        let days: [MeasuredDayOffer]
        /// Readings left out because no Apple device measured them — counted, so a sync that
        /// finds every reading foreign says so rather than quietly storing nothing.
        let fromOtherApps: Int
    }

    /// Each Apple device's readings of each figure, one entry per calendar day in `calendar`;
    /// readings another app wrote are left out (`MeasuringDevice.measuring`).
    static func reduced(_ readings: [HealthReading], in calendar: Calendar = .current) -> Reduced {
        let measured = readings.compactMap { reading in
            MeasuringDevice.measuring(reading.source).map { (reading, device: $0) }
        }
        return Reduced(days: days(of: measured, in: calendar), fromOtherApps: readings.count - measured.count)
    }

    private static func days(
        of measured: [(reading: HealthReading, device: MeasuringDevice)], in calendar: Calendar
    ) -> [MeasuredDayOffer] {
        let byDay = Dictionary(grouping: measured) { pair in
            DayKey(metric: pair.reading.metric, device: pair.device, day: CalendarDay(pair.reading.at, in: calendar))
        }
        return byDay
            .map { key, pairs in
                let values = pairs.map(\.reading.value)
                return MeasuredDayOffer(
                    metric: key.metric,
                    device: key.device,
                    measured: MeasuredDay(
                        day: key.day,
                        mean: values.reduce(0, +) / Double(values.count),
                        minimum: values.min() ?? 0,
                        maximum: values.max() ?? 0,
                        count: values.count))
            }
            .sorted { ($0.measured.day, $0.metric.rawValue, $0.device.rawValue) < ($1.measured.day, $1.metric.rawValue, $1.device.rawValue) }
    }

    private struct DayKey: Hashable {
        let metric: HealthMetric
        let device: MeasuringDevice
        let day: CalendarDay
    }
}

/// One weigh-in as Health holds it, in kilograms, Health's own unit for mass.
struct BodyMassReading: Equatable, Sendable {
    let at: Date
    let kilograms: Double
}

extension WeighIn {
    /// The weigh-in on the calendar day it was taken. Every one is offered, whoever wrote it — a
    /// scale, the Health app, an app copying the weight in; which counts is the server's rule.
    init(_ reading: BodyMassReading, in calendar: Calendar = .current) {
        self.init(day: CalendarDay(reading.at, in: calendar), at: reading.at, kilograms: reading.kilograms)
    }
}
