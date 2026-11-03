import Foundation

/// One focus pin read against today and the movement's history: where the horizon stands, how
/// often the movement has been trained against what the pin asked for, how it has moved since the
/// pin, and how far the target is.
///
/// Every figure here is a reading of the log, which is why none of it is stored on the server: a
/// session logged late, or deleted, changes the reading and could never leave a stored "met"
/// disagreeing with it.
struct FocusReading: Equatable, Sendable, Identifiable {
    let pin: FocusPin
    let phase: Phase
    let attendance: Attendance
    /// The movement's most recent session, inside the horizon or not.
    let lastDone: CalendarDay?
    /// Estimated one-rep max before the pin against since, in the unit the pin is read in.
    let progress: Progress?
    let target: TargetStanding

    var id: UUID { pin.focusPinId }

    /// Where a pin's horizon stands today.
    enum Phase: Equatable, Sendable {
        /// Pinned for a horizon that has not begun.
        case upcoming(startsInDays: Int)
        /// Inside the horizon: the day of it this is, and how many it has.
        case running(day: Int, of: Int)
        /// Still pinned, with the horizon behind it.
        case horizonOver
        /// Taken off the list on a day the athlete chose.
        case unpinned(on: CalendarDay)
        /// A status this build cannot read.
        case unrecognised

        /// For a running pin, the week of the horizon this is and how many weeks it spans.
        var week: (current: Int, of: Int)? {
            guard case .running(let day, let days) = self else { return nil }
            return ((day - 1) / 7 + 1, Int((Double(days) / 7).rounded(.up)))
        }

        /// Whether the pin belongs with the ones being worked on, rather than with the past ones.
        var isCurrent: Bool {
            switch self {
            case .upcoming, .running: true
            case .horizonOver, .unpinned, .unrecognised: false
            }
        }
    }

    /// Sessions on the movement inside the horizon so far, against the pin's frequency.
    struct Attendance: Equatable, Sendable {
        let sessions: Int
        /// Days of the horizon gone by: from its start to today, its end or the day it was unpinned,
        /// whichever is first.
        let elapsedDays: Int
        let horizonDays: Int
        let sessionsPerWeek: Int

        /// The sessions the frequency asks for over the days gone by.
        var expectedSoFar: Double { Double(sessionsPerWeek) * Double(elapsedDays) / 7 }
        /// The sessions the frequency asks for over the whole horizon.
        var expectedOverHorizon: Int {
            Int((Double(sessionsPerWeek) * Double(horizonDays) / 7).rounded())
        }
        /// Sessions a week so far, or nothing before the horizon has begun.
        var perWeek: Double? {
            elapsedDays > 0 ? Double(sessions) / (Double(elapsedDays) / 7) : nil
        }
    }

    struct Progress: Equatable, Sendable {
        let unit: WeightUnit
        /// The movement's last session before the pin's horizon began.
        let before: MovementPoint?
        /// The latest session inside the horizon.
        let latest: MovementPoint?
        /// The best session inside the horizon.
        let best: MovementPoint?

        /// The best since the pin against where it started, in the unit.
        var change: Double? {
            guard let before, let best else { return nil }
            return best.value - before.value
        }
    }

    /// Where a target stands inside the horizon.
    enum TargetStanding: Equatable, Sendable {
        case untargeted
        /// Done: the first session in the horizon with a set at the load for the reps, or more.
        case met(on: CalendarDay, weight: Double, reps: Int)
        /// Not yet. `bestAtReps` is the heaviest load lifted for the target's reps or more inside
        /// the horizon, absent when no set has reached that many reps there.
        case toGo(weight: Double, unit: WeightUnit, reps: Int, bestAtReps: Double?)
        /// A target this build cannot read.
        case unreadable

        /// The load still to find, where there is a figure to give.
        var remaining: Double? {
            guard case .toGo(let weight, _, _, let best?) = self else { return nil }
            return max(0, weight - best)
        }
    }

    static func of(
        _ pin: FocusPin, history: MovementHistory, today: CalendarDay, calendar: Calendar = .current
    ) -> FocusReading {
        let horizon = pin.horizon
        let windowEnd = min(closing(pin, today: today), horizon.ends)
        let inWindow = history.sessions.filter { $0.day >= horizon.starts && $0.day <= windowEnd }
        let elapsed = windowEnd < horizon.starts ? 0 : horizon.starts.days(until: windowEnd, in: calendar) + 1

        return FocusReading(
            pin: pin,
            phase: phase(of: pin, today: today, calendar: calendar),
            attendance: Attendance(
                sessions: inWindow.count,
                elapsedDays: elapsed,
                horizonDays: horizon.dayCount(in: calendar),
                sessionsPerWeek: pin.sessionsPerWeek),
            lastDone: history.lastDay,
            progress: progress(of: pin, history: history, inWindow: inWindow),
            target: target(of: pin, inWindow: inWindow))
    }

    /// The last day the pin's reading runs to: today, or the day it was unpinned.
    private static func closing(_ pin: FocusPin, today: CalendarDay) -> CalendarDay {
        switch pin.status {
        case .pinned, .unrecognised: today
        case .unpinned(let on): min(on, today)
        }
    }

    static func phase(of pin: FocusPin, today: CalendarDay, calendar: Calendar = .current) -> Phase {
        switch pin.status {
        case .unpinned(let on):
            return .unpinned(on: on)
        case .unrecognised:
            return .unrecognised
        case .pinned:
            if today < pin.horizon.starts {
                return .upcoming(startsInDays: today.days(until: pin.horizon.starts, in: calendar))
            }
            if today > pin.horizon.ends { return .horizonOver }
            return .running(
                day: pin.horizon.starts.days(until: today, in: calendar) + 1,
                of: pin.horizon.dayCount(in: calendar))
        }
    }

    private static func progress(
        of pin: FocusPin, history: MovementHistory, inWindow: [MovementHistory.Session]
    ) -> Progress? {
        let unit: WeightUnit? = switch pin.target {
        case .lift(_, let unit, _): unit
        case .none, .unrecognised: history.units.first
        }
        guard let unit else { return nil }

        func point(_ session: MovementHistory.Session) -> MovementPoint? {
            MovementHistory.best(.estimatedOneRepMax, of: session.lifts(in: unit)).map {
                MovementPoint(
                    sessionId: session.sessionId, day: session.day,
                    value: $0.value, weight: $0.lift.weight, reps: $0.lift.reps)
            }
        }

        // Sessions are newest first, so the first before the horizon with an estimate is the last
        // estimable one before it — a bodyweight session or one of long sets just before the pin
        // says nothing about where the lift stood, and must not hide the one before that.
        let before = history.sessions.lazy
            .filter { $0.day < pin.horizon.starts }
            .compactMap(point)
            .first
        let since = inWindow.compactMap(point)
        let reading = Progress(
            unit: unit,
            before: before,
            latest: since.first,
            best: since.max { $0.value < $1.value })
        return reading.before == nil && reading.latest == nil ? nil : reading
    }

    private static func target(of pin: FocusPin, inWindow: [MovementHistory.Session]) -> TargetStanding {
        switch pin.target {
        case .none:
            return .untargeted
        case .unrecognised:
            return .unreadable
        case .lift(let weight, let unit, let reps):
            let met = inWindow.reversed().lazy.compactMap { session in
                session.lifts(in: unit)
                    .filter { $0.weight >= weight && $0.reps >= reps }
                    .max { ($0.weight, $0.reps) < ($1.weight, $1.reps) }
                    .map { (session.day, $0) }
            }.first
            if let met {
                return .met(on: met.0, weight: met.1.weight, reps: met.1.reps)
            }
            let best = inWindow.flatMap { $0.lifts(in: unit) }.filter { $0.reps >= reps }.map(\.weight).max()
            return .toGo(weight: weight, unit: unit, reps: reps, bestAtReps: best)
        }
    }
}

extension Array where Element == FocusReading {
    /// The pins being worked on, soonest to end first, then the past ones, most recently ended first.
    func arrangedForReading() -> (current: [FocusReading], past: [FocusReading]) {
        let current = filter(\.phase.isCurrent).sorted { $0.pin.horizon.ends < $1.pin.horizon.ends }
        let past = filter { !$0.phase.isCurrent }.sorted { $0.closedOn > $1.closedOn }
        return (current, past)
    }
}

private extension FocusReading {
    /// When a past pin stopped being worked on: the day it was unpinned, or its horizon's end.
    var closedOn: CalendarDay {
        switch phase {
        case .unpinned(let on): on
        case .upcoming, .running, .horizonOver, .unrecognised: pin.horizon.ends
        }
    }
}
