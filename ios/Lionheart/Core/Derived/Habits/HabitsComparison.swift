import Foundation

/// One habit figure for weekdays and for weekends, this span against the span before.
///
/// The server computes every mean; this names the figures, orders them, and says which direction is
/// the better one, so the screen can mark a change that went the wrong way without inventing a
/// threshold of its own.
struct HabitsComparison: Equatable, Sendable, Identifiable {
    let measure: Measure
    let weekdays: SpanComparison
    let weekends: SpanComparison

    var id: Measure { measure }

    enum Measure: CaseIterable, Hashable, Sendable {
        case steps, activeMinutes, sittingMinutes, longestSitting, longSittings
        case bedtime, wakeTime, bedtimeSpread, wakeSpread

        var name: String {
            switch self {
            case .steps: "Steps"
            case .activeMinutes: "Active"
            case .sittingMinutes: "Sedentary"
            case .longestSitting: "Longest sitting"
            case .longSittings: "Sittings ≥ 1 h"
            case .bedtime: "Bedtime"
            case .wakeTime: "Wake"
            case .bedtimeSpread: "Bedtime spread"
            case .wakeSpread: "Wake spread"
            }
        }

        /// Whether lower is the better direction. Bedtime and wake time have no better direction —
        /// a habit, not a target — and are never marked.
        var polarity: RecoveryMetric.Polarity? {
            switch self {
            case .steps, .activeMinutes: .higherIsBetter
            case .sittingMinutes, .longestSitting, .longSittings, .bedtimeSpread, .wakeSpread: .lowerIsBetter
            case .bedtime, .wakeTime: nil
            }
        }

        var isClockTime: Bool {
            switch self {
            case .bedtime, .wakeTime: true
            case .steps, .activeMinutes, .sittingMinutes, .longestSitting, .longSittings,
                 .bedtimeSpread, .wakeSpread: false
            }
        }

        fileprivate func value(_ figures: HabitsFigures) -> Double? {
            switch self {
            case .steps: figures.steps
            case .activeMinutes: figures.activeMinutes
            case .sittingMinutes: figures.sedentaryMinutes
            case .longestSitting: figures.longestSittingMinutes
            case .longSittings: figures.longSittings
            case .bedtime: figures.sleep?.bedtimeMinutesAfterNoon
            case .wakeTime: figures.sleep?.wakeMinutesAfterMidnight
            case .bedtimeSpread: figures.sleep?.bedtimeSpreadMinutes
            case .wakeSpread: figures.sleep?.wakeSpreadMinutes
            }
        }
    }

    static func of(_ report: HabitsReport, measures: [Measure] = Measure.allCases) -> [HabitsComparison] {
        measures.map { measure in
            HabitsComparison(
                measure: measure,
                weekdays: .init(
                    recent: measure.value(report.current.weekdays),
                    before: measure.value(report.previous.weekdays)),
                weekends: .init(
                    recent: measure.value(report.current.weekends),
                    before: measure.value(report.previous.weekends)))
        }
    }

    /// Whether a change went the worse way: the one thing on the table worth colouring.
    static func isWorse(_ comparison: SpanComparison, _ polarity: RecoveryMetric.Polarity?) -> Bool {
        guard let polarity, let change = comparison.change else { return false }
        return switch polarity {
        case .higherIsBetter: change < 0
        case .lowerIsBetter: change > 0
        }
    }
}

/// The weekend's sleep midpoint against the weekdays' — "social jet lag", the plain measure of how
/// far a weekend moves the body clock. Minutes, positive when the weekend sleeps later.
enum SocialJetLag {
    static func minutes(_ figures: HabitsSummary) -> Double? {
        guard let weekday = figures.weekdays.sleep?.midpointMinutesAfterMidnight,
              let weekend = figures.weekends.sleep?.midpointMinutesAfterMidnight
        else { return nil }
        return weekend - weekday
    }
}

/// One stacked segment of the hour-by-hour chart.
struct HourSegment: Equatable, Sendable, Identifiable {
    let hour: Int
    /// Where the hour sits on an axis that starts at 04:00, the way Oura's activity day does:
    /// 0 for 04:00 through 23 for 03:00.
    let position: Int
    let kind: Kind
    let minutes: Double

    var id: String { "\(hour)-\(kind)" }

    /// Bottom to top, the order the stack is drawn in.
    enum Kind: CaseIterable, Hashable, Sendable {
        case resting, sitting, low, medium, high

        var name: String {
            switch self {
            case .resting: "Resting"
            case .sitting: "Sitting"
            case .low: "Low"
            case .medium: "Medium"
            case .high: "High"
            }
        }
    }

    /// The hour on the activity day's axis: 04:00 first.
    static func position(of hour: Int) -> Int { (hour - 4 + 24) % 24 }

    static func of(_ hours: [HourOfDay]) -> [HourSegment] {
        hours.flatMap { hour in
            Kind.allCases.map { kind in
                let minutes = switch kind {
                case .resting: hour.resting
                case .sitting: hour.sitting
                case .low: hour.low
                case .medium: hour.medium
                case .high: hour.high
                }
                return HourSegment(hour: hour.hour, position: position(of: hour.hour), kind: kind, minutes: minutes)
            }
        }
    }
}

/// One lane of a day's activity timeline — what a stretch of the day is drawn as.
enum ActivityLane: CaseIterable, Hashable, Sendable {
    // Top to bottom.
    case high, medium, low, inactive, rest, asleep

    var name: String {
        switch self {
        case .high: "High"
        case .medium: "Medium"
        case .low: "Low"
        case .inactive: "Inactive"
        case .rest: "Rest"
        case .asleep: "Asleep"
        }
    }

    /// The lane a run belongs in, or `nil` for a stretch with nothing to draw — the ring off the
    /// finger, or a class Oura has not documented.
    static func of(_ run: ActivityRun) -> ActivityLane? {
        if run.isAsleep { return .asleep }
        return switch run.activityClass {
        case .high: .high
        case .medium: .medium
        case .low: .low
        case .inactive: .inactive
        case .rest: .rest
        case .nonWear, .unrecognised: nil
        }
    }
}
