import Foundation

/// The load reads in words: a day's everyday load against his usual, and the lead-up to an injury.
enum LoadWords {
    /// "Everyday load, yesterday", "Everyday load, today so far", or the day.
    static func everydayLabel(_ day: EverydayDay, today: CalendarDay) -> String {
        switch RelativeDay.of(day.day, today: today) {
        case .today: "Everyday load, today so far"
        case .yesterday: "Everyday load, yesterday"
        case .tomorrow, .daysAgo, .inDays, .distant: "Everyday load, \(day.day.wire)"
        }
    }

    /// "1.8× your usual, far above", "Usual", or the MET-minutes when there is no usual yet.
    static func everydayFigure(_ day: EverydayDay) -> String {
        switch day.standing {
        case .judged(_, _, let band):
            let ratio = day.ratio.map { "\(MeasurementText.plain(($0 * 10).rounded() / 10))× your usual" } ?? ""
            return switch band {
            case .farAbove: "\(ratio), far above"
            case .above: "\(ratio), above"
            case .usual: "Usual"
            case .unrecognised: ratio
            }
        case .unjudged, .unrecognised:
            return "\(MeasurementText.plain(day.metMinutes.rounded())) MET-min"
        }
    }

    /// "Push and legs paused", or that the injury names no family yet.
    static func paused(_ injury: ManagedInjury) -> String {
        injury.families.isEmpty
            ? "No family named: add one, or name a movement in a check-in"
            : "\(PatternFamily.listed(injury.families).capitalisingFirst) paused"
    }

    /// "Push up 40 % in the 4 weeks before: 11 hard sets a week against 8"; nil with nothing earlier,
    /// or a change too small to be a trend.
    static func leadUp(_ trend: FamilyTrend) -> String? {
        guard trend.earlier > 0, abs(trend.change) >= 0.1 else { return nil }
        let percent = Int((abs(trend.change) * 100).rounded())
        return "\(trend.family.name) \(trend.change > 0 ? "up" : "down") \(percent) % in the 4 weeks before: "
            + "\(MeasurementText.plain((trend.recent * 10).rounded() / 10)) hard sets a week against "
            + "\(MeasurementText.plain((trend.earlier * 10).rounded() / 10))"
    }
}
