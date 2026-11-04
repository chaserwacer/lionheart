import Foundation

/// The words for logging: set kinds, plates, last time, and the finish readout's comparisons.
extension Formatting {
    static func setKind(_ kind: SetKind) -> String {
        switch kind {
        case .working: "Working"
        case .warmUp: "Warm-up"
        case .drop: "Drop"
        case .failure: "Failure"
        case .unrecognised(let wire): "Kind \(wire)"
        }
    }

    /// Plates per side, heaviest first and repeats counted: "45 · 45 · 10 a side".
    static func plates(_ loading: PlateLoading, unit: WeightUnit) -> String {
        switch loading {
        case .barOnly:
            return "Bar only"
        case .loaded(let perSide, let short):
            let plates = perSide.map(measure).joined(separator: " · ")
            let side = "\(plates) a side"
            return short > 0 ? "\(side), \(weight(short, unit)) under" : side
        }
    }

    /// A previous set, compactly: "160 lb × 8" or a distance in a time.
    static func lastTime(_ entry: SetEntry) -> String? {
        entry.command.flatMap(set)
    }

    /// Where the fields stand against last time: "level", "+1 rep", "+5 lb", "e1RM −3 lb".
    static func versus(_ versus: SetGuide.Versus, unit: WeightUnit) -> String? {
        switch versus {
        case .same: "level"
        case .reps(let reps): "\(signedCount(reps)) \(abs(reps) == 1 ? "rep" : "reps")"
        case .load(let load): signedWeight(load, unit)
        case .estimate(let change): "e1RM \(signedWeight((change * 10).rounded() / 10, unit))"
        case .incomparable: nil
        }
    }

    /// A weight change in pounds with its sign: "+2.5 lb", "−1 lb", "±0 lb".
    static func signedWeight(_ value: Double, _ unit: WeightUnit) -> String {
        let pounds = Imperial.pounds(value, from: unit)
        let parts = weightParts(abs(value), unit)
        let sign = pounds > 0 ? "+" : pounds < 0 ? "−" : "±"
        return parts.unit.isEmpty ? "\(sign)\(parts.amount)" : "\(sign)\(parts.amount) \(parts.unit)"
    }

    /// A count change with its sign: "+2", "−1", "±0".
    static func signedCount(_ value: Int) -> String {
        value > 0 ? "+\(value)" : value < 0 ? "−\(-value)" : "±0"
    }

    /// A fraction as a signed percentage: `0.042` is "+4.2%".
    static func percentChange(_ fraction: Double) -> String {
        let percent = fraction * 100
        let rounded = (percent * 10).rounded() / 10
        let magnitude = abs(rounded).formatted(.number.precision(.fractionLength(0...1)))
        return rounded > 0 ? "+\(magnitude)%" : rounded < 0 ? "−\(magnitude)%" : "±0%"
    }

    /// A duration change in minutes: "+6 min", "−12 min".
    static func durationChange(seconds: TimeInterval) -> String {
        let minutes = Int((seconds / 60).rounded())
        return "\(signedCount(minutes)) min"
    }

    /// A day as it reads inside a sentence — "today", "3 days ago", "Tue 28 Oct" — where the
    /// capitalised `day(_:)` would start a word mid-line.
    static func dayInline(_ day: CalendarDay, today: CalendarDay = CalendarDay(Date())) -> String {
        switch RelativeDay.of(day, today: today) {
        case .today: "today"
        case .yesterday: "yesterday"
        case .tomorrow: "tomorrow"
        case .daysAgo(let days): "\(days) days ago"
        case .inDays(let days): "in \(days) days"
        case .distant(let day): date(day)
        }
    }
}
