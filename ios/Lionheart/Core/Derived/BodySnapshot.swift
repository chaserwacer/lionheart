import Foundation

/// What the athlete's body is doing: the wearable's days and nights, the self-reported readings,
/// and the injuries being tracked.
///
/// One snapshot from several reads, for the same reason Today has one: the screen shows them beside
/// each other and they must be the same moment.
///
/// Days and nights are kept apart because Oura keeps them apart, and so does the body: a day is one
/// document per collection, while a night is zero to many sleep periods occupying clock time. The
/// nights are indexed by the day Oura filed them under rather than by when they began, because a
/// night starting before midnight belongs to the morning it ended on.
struct BodySnapshot: Equatable, Sendable {
    /// Newest first. Only days the ring recorded something for — a day it did not is absent from
    /// the range rather than present and zeroed, which is what lets the screen say "no data"
    /// honestly.
    let ouraDays: [OuraDay]
    /// Newest first.
    let wellness: [WellnessState]
    /// Active first, then by the day they started, newest first.
    let injuries: [Injury]
    /// The sleep periods in the window, by the day Oura filed each under.
    let nights: [CalendarDay: [OuraEvent<OuraSleepPeriod>]]
    /// The workouts the ring saw, by day.
    let ringWorkouts: [CalendarDay: [OuraEvent<OuraWorkout>]]
    /// What the athlete tagged, by day — the closest thing here to their own account of it.
    let tags: [CalendarDay: [OuraEvent<OuraTag>]]

    var isEmpty: Bool {
        ouraDays.isEmpty && wellness.isEmpty && injuries.isEmpty && nights.isEmpty
    }

    /// The most recent Oura day carrying a readiness score, which is what the top of the screen
    /// leads with.
    ///
    /// It reuses `TodaySnapshot.latestReadiness` rather than repeating the rule, so the two screens
    /// cannot make different claims about the athlete's recovery.
    var latestReadiness: ReadinessReading? { TodaySnapshot.latestReadiness(in: ouraDays) }

    var activeInjuries: [Injury] { injuries.filter(\.isActive) }
    var resolvedInjuries: [Injury] { injuries.filter { !$0.isActive } }

    /// The night's main sleep for a day, where Oura scored one.
    ///
    /// A day can hold several sleep periods — naps and stretches of rest among them — and only one
    /// is the night. Taking the longest instead would pick a long afternoon nap over a broken
    /// night, which is why Oura's own flag decides it.
    func mainSleep(on day: CalendarDay) -> OuraSleepPeriod? {
        nights[day]?.first { $0.reading.isMainSleep }?.reading
    }

    func workouts(on day: CalendarDay) -> [OuraWorkout] {
        (ringWorkouts[day] ?? []).map(\.reading)
    }

    func tags(on day: CalendarDay) -> [OuraTag] {
        (tags[day] ?? []).map(\.reading)
    }

    static func reading(
        ouraDays: [OuraDay],
        wellness: [WellnessState],
        injuries: [Injury],
        nights: [OuraEvent<OuraSleepPeriod>] = [],
        ringWorkouts: [OuraEvent<OuraWorkout>] = [],
        tags: [OuraEvent<OuraTag>] = []
    ) -> BodySnapshot {
        BodySnapshot(
            ouraDays: ouraDays.sorted { $0.date > $1.date },
            wellness: wellness.sorted { $0.date > $1.date },
            injuries: injuries.sorted { lhs, rhs in
                lhs.isActive == rhs.isActive
                    ? lhs.injuryDate > rhs.injuryDate
                    : lhs.isActive
            },
            nights: Dictionary(grouping: nights, by: \.day),
            ringWorkouts: Dictionary(grouping: ringWorkouts, by: \.day),
            tags: Dictionary(grouping: tags, by: \.day))
    }
}

/// One self-reported reading, with its components named.
///
/// `overall` is the server's own average — with stress inverted, since a low stress score is a good
/// day — and is received rather than recomputed so the two clients and the analyses cannot disagree
/// about one number.
struct WellnessReading: Equatable, Sendable {
    let day: CalendarDay
    let overall: Double
    let components: [Component]

    struct Component: Equatable, Sendable, Identifiable {
        let name: String
        /// 1–5 as the athlete entered it.
        let score: Int
        /// Whether a high score is a good day. False for stress, which is why the overall average
        /// inverts it.
        let higherIsBetter: Bool

        var id: String { name }
    }

    static let scale = 1...5

    static func of(_ state: WellnessState) -> WellnessReading {
        WellnessReading(
            day: state.date,
            overall: state.overallScore,
            components: [
                Component(name: "Mood", score: state.moodScore, higherIsBetter: true),
                Component(name: "Energy", score: state.energyScore, higherIsBetter: true),
                Component(name: "Motivation", score: state.motivationScore, higherIsBetter: true),
                Component(name: "Stress", score: state.stressScore, higherIsBetter: false),
            ])
    }
}

/// An injury's events, newest first, and the reading of it a screen leads with.
struct InjuryHistory: Equatable, Sendable {
    let injury: Injury
    /// Newest first — a tracked problem is read from the latest check-in backwards.
    let events: [InjuryEvent]

    /// The pain level at the most recent event, which is the one number that says how it is going.
    /// Absent when nothing has been logged against it yet, which is different from a pain of zero.
    var latestPain: Int? { events.first?.painLevel }

    static func of(_ injury: Injury) -> InjuryHistory {
        InjuryHistory(
            injury: injury,
            events: injury.events.sorted {
                ($0.createdAt ?? .distantPast) > ($1.createdAt ?? .distantPast)
            })
    }
}
