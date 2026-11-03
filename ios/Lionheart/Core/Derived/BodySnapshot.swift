import Foundation

/// What the athlete's body is doing: the wearable's days, the self-reported readings, and the
/// injuries being tracked.
///
/// Three reads, one snapshot, for the same reason Today has one: the screen shows them beside each
/// other and they must be the same moment.
struct BodySnapshot: Equatable, Sendable {
    /// Newest first. Only days the server actually stored — the single-day route synthesizes a
    /// zeroed placeholder for a day with no data, so a client that asked it could not tell "not
    /// recovered" from "the ring has not synced". The range route is the only honest read.
    let ouraDays: [OuraDay]
    /// Newest first.
    let wellness: [WellnessState]
    /// Active first, then by the day they started, newest first.
    let injuries: [Injury]

    var isEmpty: Bool { ouraDays.isEmpty && wellness.isEmpty && injuries.isEmpty }

    /// The most recent Oura day carrying a readiness score, which is what the top of the screen
    /// leads with.
    ///
    /// It reuses `TodaySnapshot.latestReadiness` rather than repeating the rule: a zero readiness
    /// is a row stored without that panel and not a terrible night, and two screens disagreeing
    /// about that would be two different claims about the athlete's recovery.
    var latestReadiness: ReadinessReading? { TodaySnapshot.latestReadiness(in: ouraDays) }

    var activeInjuries: [Injury] { injuries.filter(\.isActive) }
    var resolvedInjuries: [Injury] { injuries.filter { !$0.isActive } }

    static func reading(
        ouraDays: [OuraDay], wellness: [WellnessState], injuries: [Injury]
    ) -> BodySnapshot {
        BodySnapshot(
            ouraDays: ouraDays.sorted { $0.date > $1.date },
            wellness: wellness.sorted { $0.date > $1.date },
            injuries: injuries.sorted { lhs, rhs in
                lhs.isActive == rhs.isActive
                    ? lhs.injuryDate > rhs.injuryDate
                    : lhs.isActive
            })
    }
}

/// One panel of an Oura day, named and reduced to the figures a screen shows.
///
/// A panel per case rather than one struct of optionals, because the panels arrive **independently**
/// — a day can have sleep and no readiness — and because the Oura surface is being widened in
/// parallel. A new panel is a new case and a new section; it is not a rewrite of this type or of
/// the screen that draws it.
enum OuraPanel: Equatable, Sendable, Identifiable {
    case readiness(OuraDay.Readiness)
    case sleep(OuraDay.Sleep)
    case activity(OuraDay.Activity)
    case resilience(OuraDay.Resilience)

    var id: String { title }

    var title: String {
        switch self {
        case .readiness: "Readiness"
        case .sleep: "Sleep"
        case .activity: "Activity"
        case .resilience: "Resilience"
        }
    }

    /// Oura's own score for the panel, where it issues one. Resilience has no score — it has
    /// recovery components and a level — which is why this is optional rather than a zero.
    var score: Int? {
        switch self {
        case .readiness(let readiness): readiness.score
        case .sleep(let sleep): sleep.score
        case .activity(let activity): activity.score
        case .resilience: nil
        }
    }

    /// One labelled figure in a panel.
    struct Figure: Equatable, Sendable, Identifiable {
        let label: String
        let value: String

        var id: String { label }
    }

    /// What this panel has besides its score.
    ///
    /// Exhaustive over the panels, so a panel added as the Oura surface widens has to say what it
    /// shows rather than silently rendering as a bare score. The strings are measurements rather
    /// than prose, which is why they live here and not in `Support/Formatting`.
    var figures: [Figure] {
        switch self {
        case .readiness(let readiness):
            return [
                Figure(label: "Resting heart rate", value: "\(readiness.restingHeartRate)"),
                Figure(label: "HRV balance", value: "\(readiness.hrvBalance)"),
                Figure(label: "Recovery index", value: "\(readiness.recoveryIndex)"),
                Figure(label: "Sleep balance", value: "\(readiness.sleepBalance)"),
                Figure(
                    label: "Temperature deviation",
                    value: String(format: "%+.1f °C", readiness.temperatureDeviation)),
            ]
        case .sleep(let sleep):
            return [
                Figure(label: "Asleep", value: MeasurementText.spelledDuration(seconds: Double(sleep.totalSleep))),
                Figure(label: "REM", value: MeasurementText.spelledDuration(seconds: Double(sleep.remSleep))),
                Figure(label: "Deep", value: MeasurementText.spelledDuration(seconds: Double(sleep.deepSleep))),
                Figure(label: "Efficiency", value: "\(sleep.efficiency)%"),
            ]
        case .activity(let activity):
            return [
                Figure(label: "Steps", value: "\(activity.steps)"),
                Figure(label: "Active calories", value: "\(activity.activeCalories)"),
                Figure(label: "Total calories", value: "\(activity.totalCalories)"),
            ]
        case .resilience(let resilience):
            return [
                Figure(label: "Level", value: resilience.level.nonEmpty ?? "Unreported"),
                Figure(label: "Sleep recovery", value: MeasurementText.plain(resilience.sleepRecovery)),
                Figure(label: "Daytime recovery", value: MeasurementText.plain(resilience.daytimeRecovery)),
                Figure(label: "Stress", value: MeasurementText.plain(resilience.stress)),
            ]
        }
    }


    /// The panels a day actually carries, in the order the screen reads them: how recovered, how
    /// you slept, what you did, how you are holding up.
    static func panels(of day: OuraDay) -> [OuraPanel] {
        var panels: [OuraPanel] = []
        if let readiness = day.readiness { panels.append(.readiness(readiness)) }
        if let sleep = day.sleep { panels.append(.sleep(sleep)) }
        if let activity = day.activity { panels.append(.activity(activity)) }
        if let resilience = day.resilience { panels.append(.resilience(resilience)) }
        return panels
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
