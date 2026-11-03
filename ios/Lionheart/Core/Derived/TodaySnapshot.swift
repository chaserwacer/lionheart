import Foundation

/// Everything the Today screen shows, derived from one window of sessions and one window of Oura
/// days.
///
/// It is a named cluster rather than four loose properties on the store because the four answers
/// are one reading of a moment: how the body is, what was last done, what is next. A screen that
/// held them separately would be able to show a readiness from one refresh beside a session from
/// another.
struct TodaySnapshot: Equatable, Sendable {
    /// The most recent Oura day with readiness on it, and nothing when the ring has not synced.
    let readiness: ReadinessReading?
    let lastCompleted: TrainingSession.Completed?
    let nextPlanned: TrainingSession.Planned?
    /// A session the athlete is in the middle of, if the server knows about one.
    let inProgress: TrainingSession.Active?

    var isEmpty: Bool {
        readiness == nil && lastCompleted == nil && nextPlanned == nil && inProgress == nil
    }
}

/// Oura's readiness score, with the day it belongs to.
///
/// The day travels with the score because a readiness from three days ago means something different
/// from this morning's, and a screen that showed the number alone would be quietly wrong whenever
/// the ring had not synced.
///
/// The two balances are **contributor scores**, each 1–100, comparing the day against the
/// athlete's own baseline. Neither is a measurement: `restingHeartRateBalance` says how his
/// resting rate compared, not what it was. The rate itself is on the night, as
/// `OuraSleepPeriod.lowestHeartRate`, which is why these carry "balance" in their names.
struct ReadinessReading: Equatable, Sendable {
    let day: CalendarDay
    let score: Int
    let restingHeartRateBalance: Int?
    let hrvBalance: Int?
    /// Oura's sleep score for the same day, where there is one.
    let sleepScore: Int?

    /// How Oura itself bands the score, which is what the gauge colours by. Lionheart does not
    /// invent its own judgement of recovery; it reports Oura's.
    var band: Band {
        switch score {
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
    /// Reads a Today snapshot out of one session window and one Oura window.
    ///
    /// The window is one request rather than four, so everything on the screen is the same moment.
    /// `today` is injected rather than read from the clock, which is what lets this be tested.
    static func reading(
        sessions: [TrainingSession],
        ouraDays: [OuraDay],
        today: CalendarDay
    ) -> TodaySnapshot {
        TodaySnapshot(
            readiness: latestReadiness(in: ouraDays),
            lastCompleted: lastCompleted(in: sessions, onOrBefore: today),
            nextPlanned: nextPlanned(in: sessions, onOrAfter: today),
            inProgress: sessions.compactMap(\.activeCase).max { $0.startedAt < $1.startedAt })
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
                guard let readiness = day.readiness, let score = readiness.score else { return nil }
                return ReadinessReading(
                    day: day.date,
                    score: score,
                    restingHeartRateBalance: readiness.contributors.restingHeartRate,
                    hrvBalance: readiness.contributors.hrvBalance,
                    sleepScore: day.sleep?.score)
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
