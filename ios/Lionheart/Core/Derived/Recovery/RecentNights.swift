import Foundation

/// The two nights a morning is read by: last night, and the night before it.
///
/// Oura files a night under the day it ended on, so the night that began at 23:00 on the 3rd is
/// the 4th's, and **last night is the main sleep filed under the day being read** — never the
/// newest night on record standing in for one not filed yet. Before the ring has synced this
/// morning, last night is `.notFiled` and says so, with the night before beside it rather than in
/// its place: that substitution is how the night before came to read as last night.
///
/// The day is the athlete's own, taken in his calendar's time zone. A night's day is Oura's
/// attribution, carried on the event, and is never recomputed from when the night began.
struct RecentNights: Equatable, Sendable {
    /// The waking day both nights count back from: the day being read.
    let day: CalendarDay
    let lastNight: Night
    let nightBefore: Night

    /// One night, by the morning it ended on.
    enum Night: Equatable, Sendable {
        case filed(OuraEvent<OuraSleepPeriod>)
        /// Oura has filed no main sleep under this morning: the ring has not synced, or was not worn.
        case notFiled(wakingDay: CalendarDay)

        var wakingDay: CalendarDay {
            switch self {
            case .filed(let event): event.day
            case .notFiled(let day): day
            }
        }

        var event: OuraEvent<OuraSleepPeriod>? {
            switch self {
            case .filed(let event): event
            case .notFiled: nil
            }
        }
    }

    /// Neither night is filed — nothing to show.
    var isEmpty: Bool { lastNight.event == nil && nightBefore.event == nil }

    /// The filed night that ended on `day`, if it is one of the two.
    func night(wakingOn day: CalendarDay) -> OuraEvent<OuraSleepPeriod>? {
        [lastNight, nightBefore].lazy.compactMap(\.event).first { $0.day == day }
    }

    /// The two nights counted back from `day`, out of whatever window of sleep periods was read.
    static func reading(
        wakingOn day: CalendarDay, nights: [OuraEvent<OuraSleepPeriod>], calendar: Calendar = .current
    ) -> RecentNights {
        func night(_ wakingDay: CalendarDay) -> Night {
            nights.first { $0.day == wakingDay && $0.reading.isMainSleep }.map(Night.filed)
                ?? .notFiled(wakingDay: wakingDay)
        }
        return RecentNights(
            day: day, lastNight: night(day), nightBefore: night(day.adding(days: -1, in: calendar)))
    }

    /// The two nights as of an instant: today is the day the instant falls on in `calendar`'s time
    /// zone, the athlete's day rather than UTC's.
    static func asOf(
        _ instant: Date, nights: [OuraEvent<OuraSleepPeriod>], calendar: Calendar = .current
    ) -> RecentNights {
        reading(wakingOn: CalendarDay(instant, in: calendar), nights: nights, calendar: calendar)
    }
}
