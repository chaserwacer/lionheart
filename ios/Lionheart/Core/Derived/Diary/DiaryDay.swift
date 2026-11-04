import Foundation

/// One day of the athlete's training life as History's day page reads it, apart from the lifting:
/// what the ring recorded, the cardio the server counted, and his own rating of the day.
///
/// The lifting is the log's, which `HistoryStore` already holds whole; the day page reads it beside
/// this, so a correction to a session shows on the day at once without the ring's day being read
/// again.
struct DiaryDay: Equatable, Sendable {
    let day: CalendarDay
    /// The ring's documents for the day, or `nil` when it filed nothing.
    let ouraDay: OuraDay?
    /// The night's main sleep, filed under the morning it ended — this day.
    let night: OuraEvent<OuraSleepPeriod>?
    /// Every cardio session the server counted on the day, oldest first: the ring's workouts and
    /// the athlete's logged distance work, each with its heart-rate zones where the ring has them.
    let cardio: [CardioSession]
    /// What the zones are drawn from, for a session opened onto its heart-rate curve.
    let maxHeartRate: MaxHeartRate?
    /// The athlete's own rating of the day, where he logged one.
    let wellness: WellnessState?

    /// The measured recovery figures the day's line shows, in reading order.
    static let recoveryLine: [RecoveryMetric] = [
        .hrv, .lowestHeartRate, .sleepDuration, .respiratoryRate, .temperatureDeviation,
    ]

    /// The recovery line's figures the ring has for the day.
    var recovery: [(metric: RecoveryMetric, value: Double)] {
        Self.recoveryLine.compactMap { metric in
            metric.value(day: ouraDay, night: night?.reading).map { (metric, $0) }
        }
    }

    /// The ring's workouts — the cardio that is not a logged session. A logged run is shown with
    /// the session it is, and `cardio(of:)` gives its zones there.
    var ringWorkouts: [CardioSession] {
        cardio.filter { session in
            switch session.source {
            case .ringWorkout, .unrecognised: true
            case .loggedSession: false
            }
        }
    }

    /// The server's cardio reading of a logged session, where it counted it as cardio.
    func cardio(of trainingSessionId: UUID) -> CardioSession? {
        cardio.first { session in
            if case .loggedSession(let id, _, _) = session.source { return id == trainingSessionId }
            return false
        }
    }

    var isEmpty: Bool { ouraDay == nil && night == nil && cardio.isEmpty && wellness == nil }

    static func reading(
        day: CalendarDay,
        ouraDays: [OuraDay],
        nights: [OuraEvent<OuraSleepPeriod>],
        cardio: CardioReport,
        wellness: [WellnessState],
        calendar: Calendar = .current
    ) -> DiaryDay {
        DiaryDay(
            day: day,
            ouraDay: ouraDays.first { $0.date == day },
            night: nights.first { $0.day == day && $0.reading.isMainSleep },
            cardio: cardio.sessions
                .filter { CalendarDay($0.start, in: calendar) == day }
                .sorted { $0.start < $1.start },
            maxHeartRate: cardio.maxHeartRate,
            wellness: wellness.first { $0.date == day })
    }
}
