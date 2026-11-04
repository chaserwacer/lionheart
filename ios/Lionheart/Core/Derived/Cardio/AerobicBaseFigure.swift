import Foundation

/// One line of "is my aerobic base improving": a figure over the last four weeks against the eight
/// before, and which direction is the better one.
///
/// The server computes both windows; this only names the figures and their polarity, so the screen
/// can say whether a change is the good kind without the client re-deriving a number.
struct AerobicBaseFigure: Equatable, Sendable, Identifiable {
    let measure: Measure
    let comparison: SpanComparison

    var id: Measure { measure }

    enum Measure: CaseIterable, Hashable, Sendable {
        case zone2MinutesPerWeek, cardioMinutesPerWeek, vo2Max, restingHeartRate, cardiovascularAge

        var name: String {
            switch self {
            case .zone2MinutesPerWeek: "Zone 2 / week"
            case .cardioMinutesPerWeek: "Cardio / week"
            case .vo2Max: "VO₂max"
            case .restingHeartRate: "Lowest night HR"
            case .cardiovascularAge: "Cardiovascular age"
            }
        }

        /// The unit a value is read in, beside the number.
        var unit: String {
            switch self {
            case .zone2MinutesPerWeek, .cardioMinutesPerWeek: "min"
            case .vo2Max: "ml/kg/min"
            case .restingHeartRate: "bpm"
            case .cardiovascularAge: "yr"
            }
        }

        var polarity: RecoveryMetric.Polarity {
            switch self {
            case .zone2MinutesPerWeek, .cardioMinutesPerWeek, .vo2Max: .higherIsBetter
            case .restingHeartRate, .cardiovascularAge: .lowerIsBetter
            }
        }

        /// Whether the figure is Oura's model output rather than a measurement — said beside it.
        var isEstimate: Bool {
            switch self {
            case .vo2Max, .cardiovascularAge: true
            case .zone2MinutesPerWeek, .cardioMinutesPerWeek, .restingHeartRate: false
            }
        }
    }

    /// Which way the figure moved, read with its polarity. Equal is `.unchanged`; either window
    /// missing is `.unknown`, which is not the same as no change.
    enum Direction: Equatable, Sendable {
        case better, worse, unchanged, unknown
    }

    var direction: Direction {
        guard let change = comparison.change else { return .unknown }
        guard change != 0 else { return .unchanged }
        switch measure.polarity {
        case .higherIsBetter: return change > 0 ? .better : .worse
        case .lowerIsBetter: return change < 0 ? .better : .worse
        }
    }

    static func of(_ base: AerobicBase) -> [AerobicBaseFigure] {
        Measure.allCases.map { measure in
            let comparison = switch measure {
            case .zone2MinutesPerWeek: base.zone2MinutesPerWeek
            case .cardioMinutesPerWeek: base.cardioMinutesPerWeek
            case .vo2Max: base.vo2Max
            case .restingHeartRate: base.restingHeartRate
            case .cardiovascularAge: base.cardiovascularAge
            }
            return AerobicBaseFigure(measure: measure, comparison: comparison)
        }
    }
}

/// One zone's minutes in one week — a stacked bar's segment.
struct ZoneSegment: Equatable, Sendable, Identifiable {
    let weekStarting: CalendarDay
    /// 1 to 5.
    let zone: Int
    let minutes: Double

    var id: String { "\(weekStarting.wire)-\(zone)" }

    static func of(_ weeks: [CardioWeek]) -> [ZoneSegment] {
        weeks.flatMap { week in
            week.zones.byZone.enumerated().map { index, minutes in
                ZoneSegment(weekStarting: week.weekStarting, zone: index + 1, minutes: minutes)
            }
        }
    }
}

extension MaxHeartRate {
    /// The beats-per-minute floor of each of the five zones, for labelling them.
    var zoneFloors: [Int] {
        guard let bpm else { return [] }
        return ZoneMinutes.lowerEdges.map { Int((Double(bpm) * $0).rounded(.up)) }
    }
}

extension CardioSession {
    /// The session in words: "Hiking", "Trail run", "Run".
    var activityName: String {
        switch source {
        case .ringWorkout(_, let activity, _): OuraVocabulary.phrase(activity)
        case .loggedSession(_, let isTrailRun, _): isTrailRun ? "Trail run" : "Distance and time"
        case .unrecognised: "Session"
        }
    }

    var isLogged: Bool {
        switch source {
        case .loggedSession: true
        case .ringWorkout, .unrecognised: false
        }
    }
}
