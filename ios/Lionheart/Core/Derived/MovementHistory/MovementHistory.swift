import Foundation

/// Every session one movement was performed in, newest first, with that movement's sets in each.
///
/// Read from History's whole-log window rather than a route of its own: the log is already on the
/// device in full, so a movement's history is a filter over it and costs no request. A movement
/// is one `movementDataId` — a paused squat and a high-bar squat are two movements with two
/// histories, never merged by name.
struct MovementHistory: Equatable, Sendable {
    let movementDataId: UUID
    /// Newest first.
    let sessions: [Session]

    /// One session's work on the movement.
    struct Session: Equatable, Sendable, Identifiable {
        let sessionId: UUID
        let day: CalendarDay
        /// When the session began — the instant the server dates its records with.
        let startedAt: Date?
        /// The movement's sets in the order performed, each with its place in the whole session.
        let sets: [SessionDetail.IndexedSet]

        var id: UUID { sessionId }

        /// The lifts among the sets, in one unit.
        func lifts(in unit: WeightUnit) -> [SetEntry.Lift] {
            sets.compactMap { set in
                guard case .lift(let lift) = set.entry, lift.unit == unit else { return nil }
                return lift
            }
        }
    }

    var isEmpty: Bool { sessions.isEmpty }
    var lastDay: CalendarDay? { sessions.first?.day }
    var firstDay: CalendarDay? { sessions.last?.day }

    /// The weight units the movement was lifted in, the one lifted in most recently first.
    ///
    /// Weights never convert, so every figure is read in one unit at a time — the same rule the
    /// records follow.
    var units: [WeightUnit] {
        sessions.flatMap { $0.sets.compactMap(\.entry.liftUnit) }
            .reduce(into: [WeightUnit]()) { order, unit in
                if !order.contains(unit) { order.append(unit) }
            }
    }

    static func of(
        _ movementDataId: UUID, in sessions: [TrainingSession], today: CalendarDay
    ) -> MovementHistory {
        let performed = sessions.compactMap { session -> Session? in
            guard SessionLog.kind(of: session, today: today).isPerformed else { return nil }
            let sets = session.sets.enumerated()
                .filter { $0.element.movementDataId == movementDataId }
                .map { SessionDetail.IndexedSet(order: $0.offset, entry: $0.element) }
            guard !sets.isEmpty else { return nil }
            return Session(
                sessionId: session.id, day: session.date, startedAt: session.startedAt, sets: sets)
        }
        return MovementHistory(
            movementDataId: movementDataId,
            sessions: performed.sorted { ($0.day, $0.startedAt ?? .distantPast) > ($1.day, $1.startedAt ?? .distantPast) })
    }
}

/// What a movement's chart can measure, session by session.
enum MovementMeasure: Hashable, Sendable {
    /// The best estimated single of the session's sets — see `EstimatedOneRepMax`.
    case estimatedOneRepMax
    /// The heaviest load lifted, whatever the reps.
    case heaviestSet
    /// The largest weight × reps of one set.
    case bestSetVolume
    /// The most reps done at one load — how a lifter progresses at a fixed weight.
    case repsAt(load: Double)

    var label: String {
        switch self {
        case .estimatedOneRepMax: "Estimated 1RM"
        case .heaviestSet: "Heaviest set"
        case .bestSetVolume: "Best set volume"
        case .repsAt: "Reps at load"
        }
    }
}

/// One session's figure on a measure, with the set that produced it, so a point on a chart can
/// say what was lifted rather than only a number derived from it.
struct MovementPoint: Equatable, Sendable, Identifiable {
    let sessionId: UUID
    let day: CalendarDay
    let value: Double
    let weight: Double
    let reps: Int

    var id: UUID { sessionId }
}

/// A series of points read as its three figures: the latest, the best, and the change from the
/// first to the latest.
struct MovementSeriesSummary: Equatable, Sendable {
    let latest: MovementPoint
    let best: MovementPoint
    let change: Double

    /// `nil` for an empty series. Points are oldest first.
    static func of(_ points: [MovementPoint]) -> MovementSeriesSummary? {
        guard let first = points.first, let latest = points.last,
              let best = points.max(by: { $0.value < $1.value })
        else { return nil }
        return MovementSeriesSummary(latest: latest, best: best, change: latest.value - first.value)
    }
}

extension MovementHistory {
    /// The measure session by session, oldest first, in one unit. A session with nothing to
    /// measure — no lift in the unit, every set past the estimate's cap, no set at the load — has
    /// no point rather than a zero, so a gap reads as a gap.
    func series(_ measure: MovementMeasure, in unit: WeightUnit) -> [MovementPoint] {
        sessions.reversed().compactMap { session in
            Self.best(measure, of: session.lifts(in: unit)).map { best in
                MovementPoint(
                    sessionId: session.sessionId, day: session.day,
                    value: best.value, weight: best.lift.weight, reps: best.lift.reps)
            }
        }
    }

    /// The loads worth charting reps at: those used in at least two sessions, the most used first.
    /// One session at a load is a point, not a progression.
    func repeatedLoads(in unit: WeightUnit) -> [Double] {
        let sessionsAt = sessions.reduce(into: [Double: Int]()) { counts, session in
            for load in Set(session.lifts(in: unit).map(\.weight)) where load > 0 {
                counts[load, default: 0] += 1
            }
        }
        return sessionsAt
            .filter { $0.value >= 2 }
            .sorted { ($0.value, $0.key) > ($1.value, $1.key) }
            .map(\.key)
    }

    /// The best of a session's lifts on a measure, and the lift it came from.
    static func best(
        _ measure: MovementMeasure, of lifts: [SetEntry.Lift]
    ) -> (value: Double, lift: SetEntry.Lift)? {
        let scored: [(value: Double, lift: SetEntry.Lift)] = lifts.compactMap { lift in
            let value: Double? = switch measure {
            case .estimatedOneRepMax: EstimatedOneRepMax.of(weight: lift.weight, reps: lift.reps)
            case .heaviestSet: lift.weight > 0 ? lift.weight : nil
            case .bestSetVolume: lift.weight > 0 && lift.reps > 0 ? lift.weight * Double(lift.reps) : nil
            case .repsAt(let load): lift.weight == load && lift.reps > 0 ? Double(lift.reps) : nil
            }
            return value.map { ($0, lift) }
        }
        // Ties go to the set with more reps, then the heavier: "315 × 5" says more than "315 × 3".
        return scored.max { ($0.value, $0.lift.reps, $0.lift.weight) < ($1.value, $1.lift.reps, $1.lift.weight) }
    }
}

/// How often a movement is trained: sessions per week since it was first done, and lately.
///
/// The number the athlete's own analysis turned on — the weighted dip trained 0.19 times a week —
/// stated as a measurement, against nothing but itself.
struct MovementFrequency: Equatable, Sendable {
    let sessionCount: Int
    /// The first session's day, which the overall rate is measured from.
    let since: CalendarDay?
    /// Weekly counts, oldest first, empty weeks included: from the week of the first session
    /// through this one.
    let weeks: [TrainingWeek]

    /// The recent window: a quarter, long enough that one missed week does not swing it.
    static let recentWeeks = 12

    /// Sessions a week over every week since the first, this one included — the same weeks the
    /// chart draws, so the figure and the bars cannot disagree.
    var perWeek: Double { Self.rate(of: weeks) }

    /// Sessions a week over the last `recentWeeks` weeks, this one included.
    var recentPerWeek: Double {
        let recent = weeks.suffix(Self.recentWeeks)
        return Double(recent.reduce(0) { $0 + $1.sessionCount }) / Double(Self.recentWeeks)
    }

    static func rate(of weeks: some Collection<TrainingWeek>) -> Double {
        weeks.isEmpty ? 0 : Double(weeks.reduce(0) { $0 + $1.sessionCount }) / Double(weeks.count)
    }

    static func of(
        _ history: MovementHistory, today: CalendarDay, calendar: Calendar = .current
    ) -> MovementFrequency {
        MovementFrequency(
            sessionCount: history.sessions.count,
            since: history.firstDay,
            weeks: TrainingTrends.weeks(
                ofDays: history.sessions.map(\.day),
                setCounts: history.sessions.map(\.sets.count),
                today: today, calendar: calendar))
    }
}

extension SetEntry {
    /// The unit a lift was loaded in; nothing for any other shape.
    var liftUnit: WeightUnit? {
        guard case .lift(let lift) = self else { return nil }
        return lift.unit
    }
}

extension TrainingSession {
    /// When the session began, for the two states that began.
    var startedAt: Date? {
        switch self {
        case .completed(let completed): completed.period.startTime
        case .active(let active): active.startedAt
        case .planned, .skipped, .unrecognised: nil
        }
    }

    /// When a completed session ended, where its period is an interval.
    var endedAt: Date? {
        guard case .completed(let completed) = self, case .interval(_, let end) = completed.period
        else { return nil }
        return end
    }
}
