import Foundation

/// The week's expectations in words: a priority aim and a non-training schedule.
enum ExpectationWords {
    /// "Step to 5 × 205 lb", "Hold 5 × 200 lb", "Work up to a top set of 5"; `nil` for an aim this
    /// build cannot read.
    static func aim(_ aim: LiftAim) -> String? {
        switch aim {
        case .beat(let load, let reps, let unit, _, _, let onSystemLoad):
            "Step to \(reps) × \(Formatting.weight(load, unit))\(onSystemLoad ? " system load" : "")"
        case .hold(let load, let reps, let unit, _, let onSystemLoad):
            "Hold \(reps) × \(Formatting.weight(load, unit))\(onSystemLoad ? " system load" : "")"
        case .establish:
            "Work up to a top set of 5"
        case .unrecognised:
            nil
        }
    }

    /// "Tue, Nov 4", "Every Sunday from Nov 8", "Every Sunday, Nov 8 – Dec 6".
    static func schedule(_ schedule: NonTrainingSchedule) -> String {
        switch schedule {
        case .oneDay(let day):
            Formatting.date(day)
        case .everyWeek(let from):
            "Every \(weekday(from)) from \(monthDay(from))"
        case .everyWeekUntil(let from, let until):
            "Every \(weekday(from)), \(monthDay(from)) – \(monthDay(until))"
        case .unrecognised:
            "Not readable by this version"
        }
    }

    static func weekday(_ day: CalendarDay) -> String {
        day.middayDate().formatted(.dateTime.weekday(.wide))
    }

    private static func monthDay(_ day: CalendarDay) -> String {
        day.middayDate().formatted(.dateTime.month(.abbreviated).day())
    }
}
