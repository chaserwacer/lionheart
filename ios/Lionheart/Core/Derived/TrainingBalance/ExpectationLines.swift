import Foundation

/// One of the week's expectations as Train lists it: what was done against the adjusted target, the
/// raw target beside it when the days marked off lowered it, and the server's reasons.
struct ExpectationLine: Equatable, Sendable, Identifiable {
    enum Measure: Equatable, Sendable {
        case sessions
        case hardSets(PatternFamily)
        case minutes(ZoneBand)
    }

    let measure: Measure
    let expectation: Expectation

    var id: String {
        switch measure {
        case .sessions: "sessions"
        case .hardSets(let family): "sets.\(family.wire)"
        case .minutes(let band): "minutes.\(band.name)"
        }
    }

    /// Whether what was done reaches the adjusted target. A week that asks nothing has nothing to meet.
    var isMet: Bool { expectation.asksAnything && expectation.done >= expectation.adjusted.target }
}

enum ExpectationLines {
    /// Sessions first, then push, pull and legs, then zone 2 and quality — the order the score's
    /// parts read in. Cardio drops out when the week's heart rate could not be read.
    static func of(_ expectations: WeekExpectations) -> [ExpectationLine] {
        [ExpectationLine(measure: .sessions, expectation: expectations.sessions)]
            + expectations.families.map { ExpectationLine(measure: .hardSets($0.family), expectation: $0.sets) }
            + expectations.cardio.zones.map { ExpectationLine(measure: .minutes($0.band), expectation: $0.minutes) }
    }
}

/// How the window's days stood, for the line beside the adjusted score.
enum WeekAvailability: Equatable, Sendable {
    /// Every day of the window was available: the adjusted score is the score, unless days were
    /// marked off in the three blocks before it, which consistency also reads.
    case everyDay
    /// Some days were marked off; `notes` are the notes they carry, each once, in the order met.
    case short(available: Int, of: Int, off: [CalendarDay], notes: [String])

    static func of(_ calendar: AvailabilityCalendar) -> WeekAvailability {
        let off = calendar.unavailable
        guard !off.isEmpty else { return .everyDay }
        let notes = off.reduce(into: [String]()) { notes, day in
            if case .nonTraining(_, _, let note, _) = day, !note.isEmpty, !notes.contains(note) { notes.append(note) }
        }
        return .short(available: calendar.availableDays, of: calendar.days.count, off: off.map(\.day), notes: notes)
    }
}
