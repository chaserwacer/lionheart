import Foundation

/// Gait before an injury against gait after it — asymmetry and double support, the two figures
/// that rise when a leg is hurt: the body shortens its time on the sore side and keeps both feet
/// down for longer.
struct InjuryGait: Equatable, Sendable {
    let metric: HealthMetric
    /// The mean over the eight weeks before the injury, if anything was measured then.
    let before: Double?
    /// The mean over the four weeks from the injury — or up to today, for one newer than that.
    let after: Double?
    /// The mean over the last four weeks, for an injury older than that.
    let recent: Double?
    /// The days from eight weeks before the injury to eight after, for the chart.
    let around: [MeasuredDay]

    static let beforeDays = 56
    static let afterDays = 28

    static func of(_ metric: HealthMetric, _ days: [MeasuredDay], injuredOn injury: CalendarDay, today: CalendarDay) -> InjuryGait {
        func mean(from: CalendarDay, through: CalendarDay) -> Double? {
            let held = days.filter { $0.day >= from && $0.day <= through }
            return held.isEmpty ? nil : held.map(\.mean).reduce(0, +) / Double(held.count)
        }
        let afterEnd = min(injury.adding(days: afterDays - 1), today)
        let recentFrom = today.adding(days: 1 - afterDays)

        return InjuryGait(
            metric: metric,
            before: mean(from: injury.adding(days: -beforeDays), through: injury.adding(days: -1)),
            after: mean(from: injury, through: afterEnd),
            recent: recentFrom > afterEnd ? mean(from: recentFrom, through: today) : nil,
            around: days.filter { $0.day >= injury.adding(days: -beforeDays) && $0.day <= injury.adding(days: beforeDays) })
    }

    /// After against before, in the figure's own unit; `nil` without both.
    var change: Double? {
        guard let before, let after else { return nil }
        return after - before
    }

    var hasReadings: Bool { before != nil || after != nil || recent != nil }
}
