import Foundation

/// How much training happened in one week.
///
/// Set count and session count, and deliberately **not** tonnage. The server derives volume for a
/// personal record (`PersonalRecord.Volume`) but derives nothing for a session, so a tonnage figure
/// here would be a number only this screen believes — and summing a kilogram set with a pound one
/// would make it a wrong number as well as a lonely one. Counts are readings of what the server
/// sent, so two screens cannot disagree about them.
struct TrainingWeek: Equatable, Sendable, Identifiable {
    /// Monday of the week, which is what the chart's axis is labelled by.
    let start: CalendarDay
    let sessionCount: Int
    let setCount: Int

    var id: CalendarDay { start }
}

/// A movement's heaviest set, session by session.
///
/// The one progression a lifter actually reads, and a pure maximum over weights the server stated —
/// no derivation to disagree with. Series are keyed by movement **and unit**, so an athlete who
/// switched from pounds to kilograms gets two series rather than one line with a cliff in it.
struct StrengthSeries: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let movementName: String
    let unit: WeightUnit
    let points: [Point]

    var id: String { "\(movementDataId.uuidString)-\(unit.abbreviation)" }

    /// The heaviest set of one session, and the reps it was done for — because 100 kg for one is a
    /// different claim from 100 kg for five, and a point without its reps invites the wrong
    /// comparison.
    struct Point: Equatable, Sendable, Identifiable {
        let day: CalendarDay
        let weight: Double
        let reps: Int

        var id: CalendarDay { day }
    }

    /// Enough points to be a line rather than a dot. One session is not a trend, and drawing it as
    /// one is how a chart lies about having something to say.
    static let minimumPoints = 2
}

/// Everything the trends screen draws, from the same session window the log reads.
///
/// One derivation rather than two reads, so the weekly counts and the strength lines cannot come
/// from different moments.
struct TrainingTrends: Equatable, Sendable {
    let weeks: [TrainingWeek]
    /// Longest series first: the movement with the most history is the one worth looking at.
    let strength: [StrengthSeries]

    var isEmpty: Bool { weeks.isEmpty && strength.isEmpty }
}

extension TrainingTrends {
    static func reading(
        sessions: [TrainingSession],
        catalogue: MovementCatalogue,
        today: CalendarDay,
        calendar: Calendar = .current
    ) -> TrainingTrends {
        let performed = sessions.filter { SessionLog.kind(of: $0, today: today).isPerformed }
        return TrainingTrends(
            weeks: weeks(of: performed, today: today, calendar: calendar),
            strength: strengthSeries(of: performed, catalogue: catalogue))
    }

    /// Weekly counts, oldest first, with weeks that saw no training included as zeroes.
    ///
    /// The gaps matter: a bar chart that silently skips an empty week shows unbroken training where
    /// there was a fortnight off, which is the opposite of what the chart is for.
    static func weeks(
        of sessions: [TrainingSession], today: CalendarDay, calendar: Calendar = .current
    ) -> [TrainingWeek] {
        let counted = sessions.reduce(into: [CalendarDay: (sessions: Int, sets: Int)]()) { tally, session in
            guard let start = weekStart(of: session.date, calendar: calendar) else { return }
            let running = tally[start] ?? (0, 0)
            tally[start] = (running.sessions + 1, running.sets + session.sets.count)
        }

        guard let earliest = counted.keys.min(), let thisWeek = weekStart(of: today, calendar: calendar)
        else { return [] }

        return weekStarts(from: earliest, through: thisWeek, calendar: calendar).map { start in
            let tally = counted[start] ?? (0, 0)
            return TrainingWeek(start: start, sessionCount: tally.sessions, setCount: tally.sets)
        }
    }

    static func strengthSeries(
        of sessions: [TrainingSession], catalogue: MovementCatalogue
    ) -> [StrengthSeries] {
        // One pass over every lift, keyed by the movement and the unit it was loaded in, keeping
        // the heaviest set of each day.
        var heaviest: [SeriesKey: [CalendarDay: StrengthSeries.Point]] = [:]

        for session in sessions {
            for case .lift(let lift) in session.sets {
                let key = SeriesKey(movementDataId: lift.movementDataId, unit: lift.unit)
                let point = StrengthSeries.Point(
                    day: session.date, weight: lift.weight, reps: lift.reps)
                let standing = heaviest[key, default: [:]][session.date]
                if standing == nil || lift.weight > (standing?.weight ?? 0) {
                    heaviest[key, default: [:]][session.date] = point
                }
            }
        }

        return heaviest
            .map { key, points in
                StrengthSeries(
                    movementDataId: key.movementDataId,
                    movementName: catalogue.name(for: key.movementDataId),
                    unit: key.unit,
                    points: points.values.sorted { $0.day < $1.day })
            }
            .filter { $0.points.count >= StrengthSeries.minimumPoints }
            .sorted { lhs, rhs in
                lhs.points.count == rhs.points.count
                    ? lhs.movementName < rhs.movementName
                    : lhs.points.count > rhs.points.count
            }
    }

    /// Monday of the week `day` falls in, in the device's calendar.
    static func weekStart(of day: CalendarDay, calendar: Calendar = .current) -> CalendarDay? {
        var mondayFirst = calendar
        mondayFirst.firstWeekday = 2
        let moment = day.middayDate(in: calendar)
        guard let week = mondayFirst.dateInterval(of: .weekOfYear, for: moment) else { return nil }
        return CalendarDay(week.start.addingTimeInterval(12 * 3600), in: calendar)
    }

    private static func weekStarts(
        from first: CalendarDay, through last: CalendarDay, calendar: Calendar
    ) -> [CalendarDay] {
        var starts: [CalendarDay] = []
        var cursor = first
        // A session dated in the future would otherwise run this past `last` forever; the guard is
        // the loop's bound, not a belt-and-braces check.
        while cursor <= last, starts.count < 520 {
            starts.append(cursor)
            guard let next = calendar.date(
                byAdding: .weekOfYear, value: 1, to: cursor.middayDate(in: calendar))
            else { break }
            cursor = CalendarDay(next, in: calendar)
        }
        return starts
    }

    private struct SeriesKey: Hashable {
        let movementDataId: UUID
        let unit: WeightUnit
    }
}
