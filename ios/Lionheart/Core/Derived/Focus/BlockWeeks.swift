import Foundation

/// A pin's horizon as the training block it is: one entry per week, each week gone by with the
/// movement's best estimated single in it, this week, and the weeks still to come — the picture
/// History leads with, three weeks building and one back reading as plainly as they were trained.
///
/// Weeks run from the horizon's first day in sevens, the same weeks `FocusReading.Phase.week`
/// counts, so "week 5 of 12" and the fifth bar are one week. The estimate is the one the rest of the
/// pin's reading uses, in the pin's unit; a week with no estimable set on the movement is a week
/// with nothing to measure, not a week at zero.
struct BlockWeeks: Equatable, Sendable {
    let weeks: [Week]
    let unit: WeightUnit

    enum Week: Equatable, Sendable {
        /// A week over: its best estimated single, or `nil` when it held no estimable set.
        case done(best: Double?)
        /// The week holding today, so far.
        case current(best: Double?)
        /// A week not begun.
        case ahead

        var best: Double? {
            switch self {
            case .done(let best), .current(let best): best
            case .ahead: nil
            }
        }
    }

    /// The heaviest week, which the others are drawn against.
    var peak: Double? { weeks.compactMap(\.best).max() }

    /// Each week's height for drawing, 0 to 1: the lightest measured week at a quarter and the
    /// heaviest at the top, so a few pounds between weeks reads as a step rather than as bars of one
    /// height on a scale from zero. `nil` for a week with nothing measured or still ahead; every
    /// measured week is full when they are all the same.
    var heights: [Double?] {
        let bests = weeks.compactMap(\.best)
        guard let low = bests.min(), let high = bests.max() else { return weeks.map { _ in nil } }
        return weeks.map { week in
            week.best.map { high > low ? Self.lowest + (1 - Self.lowest) * ($0 - low) / (high - low) : 1 }
        }
    }

    /// The height of the lightest measured week.
    static let lowest = 0.25

    /// - Returns: `nil` when the movement has no unit to read in — nothing logged on it and no
    ///   target to say one.
    static func of(
        _ pin: FocusPin, history: MovementHistory, today: CalendarDay, calendar: Calendar = .current
    ) -> BlockWeeks? {
        guard let unit = FocusReading.unit(of: pin, history: history) else { return nil }
        let horizon = pin.horizon
        let count = Int((Double(horizon.dayCount(in: calendar)) / 7).rounded(.up))
        let weeks = (0..<count).map { index -> Week in
            let starts = horizon.starts.adding(days: index * 7, in: calendar)
            let ends = min(starts.adding(days: 6, in: calendar), horizon.ends)
            if today < starts { return .ahead }
            let best = history.sessions
                .filter { $0.day >= starts && $0.day <= ends }
                .compactMap { MovementHistory.best(.estimatedOneRepMax, of: $0.lifts(in: unit))?.value }
                .max()
            return today <= ends ? .current(best: best) : .done(best: best)
        }
        return BlockWeeks(weeks: weeks, unit: unit)
    }
}
