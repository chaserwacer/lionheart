import Foundation

/// Every day of a range as training availability reads it — the server's `AvailabilityCalendar`
/// (`GET /api/training/availability`, and each week's expectations).
struct AvailabilityCalendar: Equatable, Sendable {
    let range: DayRange
    let days: [DayAvailability]
    /// The days of the range not marked non-training, as the server counted them.
    let availableDays: Int

    var unavailable: [DayAvailability] { days.filter(\.isNonTraining) }

    /// What the server says about one day; open for a day the range does not hold.
    func availability(on day: CalendarDay) -> DayAvailability {
        days.first { $0.day == day } ?? .open(day)
    }
}

/// What a day is for training.
///
/// The server decides which statement names a day: a date marked off, then a planned session, then a
/// weekday marked off every week.
enum DayAvailability: Equatable, Sendable {
    case open(CalendarDay)
    /// A session is planned on the day, so it is spoken for training.
    case planned(CalendarDay)
    /// Marked non-training by the statement named; `repeats` when that statement is a weekly one.
    case nonTraining(CalendarDay, nonTrainingDayId: UUID, note: String, repeats: Bool)
    case unrecognised(CalendarDay, String)

    var day: CalendarDay {
        switch self {
        case .open(let day), .planned(let day), .nonTraining(let day, _, _, _), .unrecognised(let day, _): day
        }
    }

    var isNonTraining: Bool {
        switch self {
        case .nonTraining: true
        case .open, .planned, .unrecognised: false
        }
    }
}

// MARK: - Decoding

extension AvailabilityCalendar: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let range = container.lenient(DayRange.self, "range") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "An availability calendar needs its range."))
        }
        let days = container.lenientList(DayAvailability.self, "days")
        self.init(
            range: range,
            days: days,
            availableDays: container.lenient("availableDays", or: days.filter { !$0.isNonTraining }.count))
    }
}

extension DayAvailability: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let day = container.day("day") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A day's availability needs its day."))
        }
        let type = container.wireType
        switch type {
        case "open":
            self = .open(day)
        case "planned":
            self = .planned(day)
        case "nonTraining":
            guard let id = container.lenient(UUID.self, "nonTrainingDayId") else {
                self = .unrecognised(day, type)
                return
            }
            self = .nonTraining(
                day, nonTrainingDayId: id, note: container.lenient("note", or: ""),
                repeats: container.lenient("repeats", or: false))
        default:
            self = .unrecognised(day, type)
        }
    }
}
