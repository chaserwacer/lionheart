import Foundation

/// A day, or a weekday every week, the athlete has said he will not train — the server's
/// `NonTrainingDayView` (`/api/training/non-training-days`).
struct NonTrainingDay: Equatable, Sendable, Identifiable {
    let nonTrainingDayId: UUID
    let schedule: NonTrainingSchedule
    /// What he said about it — "travel" — or empty.
    let note: String

    var id: UUID { nonTrainingDayId }
}

/// Which days a non-training day covers.
enum NonTrainingSchedule: Equatable, Sendable {
    case oneDay(CalendarDay)
    /// The weekday of `from`, every week from it on.
    case everyWeek(from: CalendarDay)
    /// The weekday of `from`, every week from it through `until`.
    case everyWeekUntil(from: CalendarDay, until: CalendarDay)
    case unrecognised(String)

    var repeats: Bool {
        switch self {
        case .everyWeek, .everyWeekUntil: true
        case .oneDay, .unrecognised: false
        }
    }

    /// The first day it covers; `nil` for a schedule this build cannot read.
    var first: CalendarDay? {
        switch self {
        case .oneDay(let day), .everyWeek(let day), .everyWeekUntil(let day, _): day
        case .unrecognised: nil
        }
    }
}

/// How a non-training day is asked for: the day, whether its weekday repeats, and a note.
///
/// The request's flat spelling of the server's three schedules, the way the server reads it.
struct NonTrainingDayDraft: Equatable, Sendable {
    var day: CalendarDay
    var repeatsWeekly: Bool
    /// The last day a weekly repeat covers; ignored when it does not repeat.
    var until: CalendarDay?
    var note: String

    /// One day, with the note given.
    static func once(_ day: CalendarDay, note: String = "") -> NonTrainingDayDraft {
        NonTrainingDayDraft(day: day, repeatsWeekly: false, until: nil, note: note)
    }
}

// MARK: - Decoding

extension NonTrainingDay: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "nonTrainingDayId") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A non-training day needs its id."))
        }
        self.init(
            nonTrainingDayId: id,
            schedule: container.lenient(NonTrainingSchedule.self, "schedule") ?? .unrecognised(""),
            note: container.lenient("note", or: ""))
    }
}

extension NonTrainingSchedule: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch (type, container.day("day"), container.day("from"), container.day("until")) {
        case ("oneDay", let day?, _, _): self = .oneDay(day)
        case ("everyWeek", _, let from?, _): self = .everyWeek(from: from)
        case ("everyWeekUntil", _, let from?, let until?): self = .everyWeekUntil(from: from, until: until)
        default: self = .unrecognised(type)
        }
    }
}
