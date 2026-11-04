import Foundation

/// Everything the Today screen shows for one day, derived from one window of sessions and one
/// window of Oura days and nights.
///
/// It is a named cluster rather than loose properties on the store because the answers are one
/// reading of a moment: how the body was, what was trained, what is next. A screen that held them
/// separately could show a readiness from one refresh beside a session from another — or, now that
/// the day can change, one day's recovery under another day's training.
///
/// The last, next and in-progress sessions are questions about *now*, so they are answered only
/// when the day is today; a past day is read for what happened on it.
struct TodaySnapshot: Equatable, Sendable {
    /// The day being read.
    let day: CalendarDay
    /// The ring's reading, for `day` — or, on today, for the newest day it has filed, which the
    /// screen labels when it is not today.
    let recovery: RecoveryReading?
    /// The readiness contributors picked out for the recovery's day, each beside the measurement it
    /// rests on. Empty when there is no recovery to read.
    let contributors: [ReadinessContributorReading]
    /// The newest main sleep filed on or before the recovery's day — the night its figures were
    /// read from, or the last one the ring scored when it filed a readiness without a night — with
    /// its clock time, for the hypnogram.
    let night: OuraEvent<OuraSleepPeriod>?
    /// The night that ended on `day` and the one before it, each filed or not — what the screen's
    /// night rows read, so last night is never the night before standing in for it.
    let nights: RecentNights
    /// What was trained or skipped on `day`, in the server's order. On today it leaves out what is still
    /// to come and what is in progress, which have their own answers below.
    let sessionsOnDay: [TrainingSession]
    /// Today only: the latest completed session before today, when nothing is logged today.
    let lastCompleted: TrainingSession.Completed?
    /// Today only.
    let nextPlanned: TrainingSession.Planned?
    /// Today only: a session the athlete is in the middle of, if the server knows about one.
    let inProgress: TrainingSession.Active?

    var readiness: ReadinessReading? { recovery?.readiness }

    /// A night this snapshot holds, by the day it ended on: one of the two recent nights, or the
    /// night the recovery figures were read from.
    func night(wakingOn day: CalendarDay) -> OuraEvent<OuraSleepPeriod>? {
        nights.night(wakingOn: day) ?? night.flatMap { $0.day == day ? $0 : nil }
    }

    /// The recovery figures no contributor row already shows, in their reading order.
    var otherFigures: [MetricReading] {
        let shown = Set(contributors.compactMap { reading -> RecoveryMetric? in
            if case .measured(let figure) = reading.basis { return figure.metric }
            return nil
        })
        return (recovery?.figures ?? []).filter { !shown.contains($0.metric) }
    }

    /// How far back Today will reach for a reading when the ring has not filed today. A fortnight
    /// finds a ring left in a drawer for a few days; past it, a long gap reads as no data rather
    /// than as an old score passed off as current.
    static let recoveryFallbackDays = 13

    var isEmpty: Bool {
        recovery == nil && sessionsOnDay.isEmpty && lastCompleted == nil && nextPlanned == nil
            && inProgress == nil
    }
}

/// Oura's readiness score, with the day it belongs to.
///
/// The day travels with the score because a readiness from three days ago means something different
/// from this morning's, and a screen that showed the number alone would be quietly wrong whenever
/// the ring had not synced. The figures behind it are `RecoveryReading`'s.
struct ReadinessReading: Equatable, Sendable {
    let day: CalendarDay
    let score: OuraScore

    /// How Oura itself bands the score, which is what the verdict reads by. Lionheart does not
    /// invent its own judgement of recovery; it reports Oura's.
    var band: Band {
        switch score.rounded {
        case ..<70: .payAttention
        case 70..<85: .good
        default: .optimal
        }
    }

    enum Band: Equatable, Sendable {
        case payAttention
        case good
        case optimal

        var label: String {
            switch self {
            case .payAttention: "Pay attention"
            case .good: "Good"
            case .optimal: "Optimal"
            }
        }
    }
}

extension TodaySnapshot {
    /// Reads a snapshot of `day` out of one session window and one Oura window.
    ///
    /// The windows are one request each rather than one per card, so everything on the screen is
    /// the same moment. `today` is injected rather than read from the clock, which is what lets this
    /// be tested. `day` defaults to today.
    static func reading(
        sessions: [TrainingSession],
        ouraDays: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>] = [],
        today: CalendarDay,
        day: CalendarDay? = nil
    ) -> TodaySnapshot {
        let day = min(day ?? today, today)
        let isToday = day == today
        let earliestFallback = today.adding(days: -recoveryFallbackDays)
        let recoveryDay = isToday
            ? RecoveryReading.readingDay(
                onOrBefore: today,
                days: ouraDays.filter { $0.date >= earliestFallback },
                nights: nights.filter { $0.day >= earliestFallback })
            : day
        let onDay = sessions
            .filter { $0.date == day }
            .filter { session in
                guard isToday else { return true }
                return session.plannedCase == nil && session.activeCase == nil
            }
        let performedToday = onDay.contains { $0.completedCase != nil }
        let recovery = recoveryDay.flatMap {
            RecoveryReading.reading(for: $0, days: ouraDays, nights: nights, today: today)
        }

        return TodaySnapshot(
            day: day,
            recovery: recovery,
            contributors: recovery.map {
                ReadinessContributorReading.readings(for: $0, days: ouraDays, nights: nights)
            } ?? [],
            night: recoveryDay.flatMap { recoveryDay in
                nights
                    .filter { $0.reading.isMainSleep && $0.day <= recoveryDay && $0.day >= earliestFallback }
                    .max { $0.day < $1.day }
            },
            nights: RecentNights.reading(wakingOn: day, nights: nights),
            sessionsOnDay: onDay,
            lastCompleted: isToday && !performedToday ? lastCompleted(in: sessions, onOrBefore: today) : nil,
            nextPlanned: isToday ? nextPlanned(in: sessions, onOrAfter: today) : nil,
            inProgress: isToday ? sessions.compactMap(\.activeCase).max { $0.startedAt < $1.startedAt } : nil)
    }

    /// The most recent day that actually carries a readiness score.
    ///
    /// The window is read rather than today alone because the screen shows the reading's day
    /// beside the number: an athlete who took the ring off for two days gets "78, three days ago"
    /// rather than nothing at all, and a stale reading reads as stale instead of as silence.
    ///
    /// Every score the server sends is kept, zero included. Absence is the server's answer for
    /// absence — a day it recorded nothing for is not in the range at all — so a zero here is a
    /// zero Oura issued, and discarding it would be the client hiding the athlete's own data.
    static func latestReadiness(in days: [OuraDay]) -> ReadinessReading? {
        days
            .compactMap { day -> ReadinessReading? in
                guard let score = day.readiness?.score else { return nil }
                return ReadinessReading(day: day.date, score: score)
            }
            .max { $0.day < $1.day }
    }

    /// The latest session that happened, not counting anything dated after today.
    ///
    /// A completed session *can* be dated in the future — logging after the fact is the common
    /// case and nothing stops a mistyped date — and calling tomorrow's entry "your last session"
    /// would read as a bug.
    static func lastCompleted(
        in sessions: [TrainingSession], onOrBefore today: CalendarDay
    ) -> TrainingSession.Completed? {
        sessions
            .compactMap(\.completedCase)
            .filter { $0.envelope.date <= today }
            .max { $0.envelope.date < $1.envelope.date }
    }

    /// The soonest session planned for today or later. A planned session in the past has been
    /// missed rather than scheduled, and is History's business, not Today's.
    static func nextPlanned(
        in sessions: [TrainingSession], onOrAfter today: CalendarDay
    ) -> TrainingSession.Planned? {
        sessions
            .compactMap(\.plannedCase)
            .filter { $0.plannedFor >= today }
            .min { $0.plannedFor < $1.plannedFor }
    }
}

extension TrainingSession {
    /// The completed session this is, if it is one. These exist so a derivation can say
    /// `compactMap(\.completedCase)` instead of writing the same `if case` three times.
    var completedCase: Completed? {
        switch self {
        case .completed(let session): session
        case .planned, .active, .skipped, .unrecognised: nil
        }
    }

    var plannedCase: Planned? {
        switch self {
        case .planned(let session): session
        case .active, .completed, .skipped, .unrecognised: nil
        }
    }

    var activeCase: Active? {
        switch self {
        case .active(let session): session
        case .planned, .completed, .skipped, .unrecognised: nil
        }
    }
}
