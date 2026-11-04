import Foundation
import Testing

@testable import Lionheart

/// Which night is "last night": the main sleep Oura filed under the day being read, counted in the
/// athlete's own time zone — never the newest night on record standing in for one not filed yet.
/// Invented nights throughout.
@Suite("Last night and the night before")
struct RecentNightsTests {
    private static let wednesday = CalendarDay(year: 2026, month: 3, day: 11)
    private static let tuesday = CalendarDay(year: 2026, month: 3, day: 10)
    private static let monday = CalendarDay(year: 2026, month: 3, day: 9)

    private static func calendar(_ zone: String) -> Calendar {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: zone)!
        return calendar
    }

    private static func instant(
        _ day: CalendarDay, _ hour: Int, _ minute: Int, in calendar: Calendar
    ) -> Date {
        calendar.date(from: DateComponents(
            year: day.year, month: day.month, day: day.day, hour: hour, minute: minute))!
    }

    @Test("Last night is the main sleep filed under the day; the night before is the day before's")
    func lastNightIsTheDaysMainSleep() {
        let lastNight = ARecovery.night(Self.wednesday, hrv: 64)
        let nap = ARecovery.night(Self.wednesday, hrv: 50, asleepSeconds: 2_400, isMainSleep: false)
        let before = ARecovery.night(Self.tuesday, hrv: 58)

        let nights = RecentNights.reading(
            wakingOn: Self.wednesday, nights: [ARecovery.night(Self.monday), nap, before, lastNight])

        #expect(nights.lastNight == .filed(lastNight))
        #expect(nights.nightBefore == .filed(before))
    }

    @Test("Before the ring syncs, last night is not filed — the night before never stands in for it")
    func anUnsyncedMorningSaysSo() {
        // The reported case: this morning's readiness can be filed while the night is not, and the
        // newest night on record is then the one before.
        let before = ARecovery.night(Self.tuesday)

        let nights = RecentNights.reading(wakingOn: Self.wednesday, nights: [before])

        #expect(nights.lastNight == .notFiled(wakingDay: Self.wednesday))
        #expect(nights.nightBefore == .filed(before))
        #expect(!nights.isEmpty)
        #expect(nights.night(wakingOn: Self.tuesday) == before)
        #expect(nights.night(wakingOn: Self.wednesday) == nil)
    }

    @Test("At midnight the nights move on a day: last night is then the one not slept yet")
    func midnightMovesTheNights() {
        let newYork = Self.calendar("America/New_York")
        let tuesdayNight = ARecovery.night(Self.tuesday)
        let all = [ARecovery.night(Self.monday), tuesdayNight]

        let lateTuesday = RecentNights.asOf(Self.instant(Self.tuesday, 23, 59, in: newYork), nights: all, calendar: newYork)
        let earlyWednesday = RecentNights.asOf(Self.instant(Self.wednesday, 0, 1, in: newYork), nights: all, calendar: newYork)

        #expect(lateTuesday.lastNight == .filed(tuesdayNight))
        #expect(earlyWednesday.lastNight == .notFiled(wakingDay: Self.wednesday))
        #expect(earlyWednesday.nightBefore == .filed(tuesdayNight))
    }

    @Test("The same instant is a different morning in another time zone")
    func theDayIsTheAthletes() {
        // 03:30 UTC on Wednesday is Tuesday evening in New York and Wednesday lunchtime in Tokyo.
        let instant = Self.instant(Self.wednesday, 3, 30, in: Self.calendar("UTC"))
        let all = [ARecovery.night(Self.tuesday), ARecovery.night(Self.wednesday)]

        let inNewYork = RecentNights.asOf(instant, nights: all, calendar: Self.calendar("America/New_York"))
        let inTokyo = RecentNights.asOf(instant, nights: all, calendar: Self.calendar("Asia/Tokyo"))

        #expect(inNewYork.day == Self.tuesday)
        #expect(inNewYork.lastNight.wakingDay == Self.tuesday)
        #expect(inTokyo.day == Self.wednesday)
        #expect(inTokyo.lastNight.wakingDay == Self.wednesday)
    }

    @Test("A night that began before midnight is last night by the morning it ended, not when it began")
    func aNightBelongsToItsWakingDay() {
        let newYork = Self.calendar("America/New_York")
        let filed = ARecovery.night(Self.wednesday)
        let beganTuesday = OuraEvent(
            ouraId: filed.ouraId,
            day: Self.wednesday,
            period: .interval(
                start: Self.instant(Self.tuesday, 23, 10, in: newYork),
                end: Self.instant(Self.wednesday, 6, 50, in: newYork)),
            reading: filed.reading)

        let wednesday = RecentNights.asOf(
            Self.instant(Self.wednesday, 7, 30, in: newYork), nights: [beganTuesday], calendar: newYork)
        let tuesday = RecentNights.asOf(
            Self.instant(Self.tuesday, 23, 30, in: newYork), nights: [beganTuesday], calendar: newYork)

        #expect(wednesday.lastNight == .filed(beganTuesday))
        #expect(tuesday.lastNight == .notFiled(wakingDay: Self.tuesday))
        #expect(tuesday.isEmpty)
    }

    @Test("Today's snapshot reads its nights from the shown day, whatever day the recovery figures fell back to")
    func todayReadsLastNightOfTheShownDay() {
        // Readiness filed this morning, the night not yet: the recovery reads today, and the night
        // rows say last night is not synced rather than showing the night before as last night.
        let before = ARecovery.night(Self.tuesday)
        let snapshot = TodaySnapshot.reading(
            sessions: [],
            ouraDays: [ARecovery.day(Self.wednesday), ARecovery.day(Self.tuesday)],
            nights: [ARecovery.night(Self.monday), before],
            today: Self.wednesday)

        #expect(snapshot.nights.lastNight == .notFiled(wakingDay: Self.wednesday))
        #expect(snapshot.nights.nightBefore == .filed(before))
        #expect(snapshot.night(wakingOn: Self.tuesday) == before)
    }

    @Test("Body counts its nights back from today out of the window it holds")
    func bodyReadsTheSameNights() {
        let lastNight = ARecovery.night(Self.wednesday)
        let before = ARecovery.night(Self.tuesday)
        let snapshot = BodySnapshot.reading(
            ouraDays: [], wellness: [], injuries: [], nights: [ARecovery.night(Self.monday), before, lastNight])

        let nights = snapshot.recentNights(today: Self.wednesday)

        #expect(nights.lastNight == .filed(lastNight))
        #expect(nights.nightBefore == .filed(before))
    }

    @Test("A night is named by the evening it began, and as last night or the night before only on today")
    func nightCaptions() {
        let night = RecentNights.Night.notFiled(wakingDay: Self.wednesday)
        let evening = Formatting.date(Self.tuesday)

        #expect(Formatting.nightCaption(night, isLastNight: true, isToday: true) == "Last night · \(evening)")
        #expect(Formatting.nightCaption(night, isLastNight: false, isToday: true) == "Night before · \(evening)")
        #expect(Formatting.nightCaption(night, isLastNight: true, isToday: false) == "Night of \(evening)")
    }
}
