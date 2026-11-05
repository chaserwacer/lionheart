import Foundation

/// What a tap on the non-training calendar asks for, read from the days selected before and after it.
///
/// The calendar is a set of selected days, so a tap arrives as a whole new set; this names the one
/// change in it. Unmarking a day a weekly statement covers removes the whole statement, so the screen
/// asks first.
enum NonTrainingMarking: Equatable, Sendable {
    /// A day newly selected: mark it non-training, once.
    case mark(CalendarDay)
    /// A marked day deselected: remove the statement that covers it.
    case unmark(CalendarDay, nonTrainingDayId: UUID, repeats: Bool)

    /// The days the calendar shows selected: the ones the server reads as non-training.
    static func marked(in calendar: AvailabilityCalendar) -> Set<CalendarDay> {
        Set(calendar.unavailable.map(\.day))
    }

    /// The change between two selections; `nil` when there is not exactly one day added or removed,
    /// or when a removed day is not one a statement covers.
    static func change(
        from before: Set<CalendarDay>, to after: Set<CalendarDay>, in calendar: AvailabilityCalendar
    ) -> NonTrainingMarking? {
        let added = after.subtracting(before)
        let removed = before.subtracting(after)
        switch (added.count, removed.count) {
        case (1, 0):
            return added.first.map(NonTrainingMarking.mark)
        case (0, 1):
            guard let day = removed.first,
                  case .nonTraining(_, let id, _, let repeats) = calendar.availability(on: day)
            else { return nil }
            return .unmark(day, nonTrainingDayId: id, repeats: repeats)
        default:
            return nil
        }
    }
}
