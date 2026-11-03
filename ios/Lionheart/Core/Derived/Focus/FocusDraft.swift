import Foundation

/// A pin's terms as the athlete sets them, before the server has them: the horizon, the sessions a
/// week, and the target.
///
/// `make` is the only way to build one, so terms that reach the API already hold the rules the
/// server refuses on — an ordered horizon, one to seven sessions, a target with reps in range.
/// Pinning and revising send the same terms, so they are one type.
struct FocusDraft: Equatable, Sendable {
    let horizon: FocusHorizon
    let sessionsPerWeek: Int
    let target: FocusTarget

    private init(horizon: FocusHorizon, sessionsPerWeek: Int, target: FocusTarget) {
        self.horizon = horizon
        self.sessionsPerWeek = sessionsPerWeek
        self.target = target
    }

    static let sessionsPerWeekRange = 1...7
    static let targetRepsRange = 1...30

    /// Why the form cannot be sent yet.
    enum Problem: Error, Equatable, Sendable {
        case endsBeforeItStarts
        case sessionsPerWeekOutOfRange
        case targetRepsOutOfRange
        case targetWeightNegative
    }

    static func make(
        starts: CalendarDay, ends: CalendarDay, sessionsPerWeek: Int, target: FocusTarget
    ) throws(Problem) -> FocusDraft {
        guard ends >= starts else { throw .endsBeforeItStarts }
        guard sessionsPerWeekRange.contains(sessionsPerWeek) else { throw .sessionsPerWeekOutOfRange }
        if case .lift(let weight, _, let reps) = target {
            guard weight >= 0, weight.isFinite else { throw .targetWeightNegative }
            guard targetRepsRange.contains(reps) else { throw .targetRepsOutOfRange }
        }
        return FocusDraft(
            horizon: FocusHorizon(starts: starts, ends: ends),
            sessionsPerWeek: sessionsPerWeek,
            target: target)
    }
}

/// The horizons a pin is usually set for. A start and an end are what is stored; these are the
/// lengths offered so a pin takes two taps rather than two date pickers.
enum FocusLength: CaseIterable, Hashable, Sendable, Identifiable {
    case fourWeeks
    case eightWeeks
    case twelveWeeks
    case sixMonths
    case year

    var id: Self { self }

    var label: String {
        switch self {
        case .fourWeeks: "4 weeks"
        case .eightWeeks: "8 weeks"
        case .twelveWeeks: "12 weeks"
        case .sixMonths: "6 months"
        case .year: "1 year"
        }
    }

    /// The last day of a horizon of this length that starts on `starts` — the day before the same
    /// day a length later, so eight weeks from a Monday ends on a Sunday.
    func ends(startingOn starts: CalendarDay, calendar: Calendar = .current) -> CalendarDay {
        let component: (Calendar.Component, Int) = switch self {
        case .fourWeeks: (.weekOfYear, 4)
        case .eightWeeks: (.weekOfYear, 8)
        case .twelveWeeks: (.weekOfYear, 12)
        case .sixMonths: (.month, 6)
        case .year: (.year, 1)
        }
        guard let later = calendar.date(
            byAdding: component.0, value: component.1, to: starts.middayDate(in: calendar))
        else { return starts }
        return CalendarDay(later, in: calendar).adding(days: -1, in: calendar)
    }
}
