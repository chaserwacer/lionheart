import Foundation
import Testing

@testable import Lionheart

/// What the widgets and the readiness intent show, and when the app asks to be woken for them.
@MainActor
@Suite("Today glance")
struct TodayGlanceTests {
    private static let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    private static func moment(_ day: Int, _ hour: Int, _ minute: Int = 0) -> Date {
        calendar.date(from: DateComponents(year: 2026, month: 3, day: day, hour: hour, minute: minute))!
    }

    // MARK: - Naming a day at draw time

    @Test("A glance's day is named against the moment it is drawn")
    func dayNamesFollowTheDrawingMoment() {
        let readinessDay = Self.moment(4, 12)

        #expect(TodayGlance.dayName(readinessDay, at: Self.moment(4, 7), calendar: Self.calendar) == "Today")
        // The same glance, drawn after midnight with no new data.
        #expect(TodayGlance.dayName(readinessDay, at: Self.moment(5, 0, 1), calendar: Self.calendar) == "Yesterday")
        #expect(TodayGlance.dayName(Self.moment(5, 12), at: Self.moment(4, 7), calendar: Self.calendar) == "Tomorrow")
    }

    @Test("A day within the week either side is its weekday, and beyond it its date")
    func furtherDaysAreNamedPlainly() {
        let drawn = Self.moment(10, 9)

        #expect(TodayGlance.dayName(Self.moment(6, 12), at: drawn, calendar: Self.calendar)
            == Self.moment(6, 12).formatted(.dateTime.weekday(.wide)))
        #expect(TodayGlance.dayName(Self.moment(1, 12), at: drawn, calendar: Self.calendar)
            == Self.moment(1, 12).formatted(.dateTime.month(.abbreviated).day()))
    }

    // MARK: - The glance from a snapshot

    @Test("A day with no Oura data and no training is a glance with nothing in it")
    func anEmptyDayIsAnEmptyGlance() {
        let snapshot = TodaySnapshot.reading(
            sessions: [], ouraDays: [], nights: [], today: CalendarDay(year: 2026, month: 3, day: 4),
            day: CalendarDay(year: 2026, month: 3, day: 4))

        let glance = TodayGlancePublisher.glance(of: snapshot, catalogue: MovementCatalogue([]), asOf: Self.moment(4, 7))

        #expect(glance.readiness == nil)
        #expect(glance.lastSession == nil)
        #expect(glance.nextSession == nil)
    }

    @Test("The readiness on the glance is Oura's score and band for the newest day filed")
    func theGlanceCarriesTheVerdict() throws {
        let today = CalendarDay(year: 2026, month: 3, day: 4)
        let days = try Decoding.list(OuraDay.self, """
            [\(Oura.day(date: "2026-03-03", readinessScore: "88")),
             \(Oura.day(date: "2026-03-04", readinessScore: "62"))]
            """)
        let snapshot = TodaySnapshot.reading(sessions: [], ouraDays: days, nights: [], today: today, day: today)

        let readiness = try #require(
            TodayGlancePublisher.glance(of: snapshot, catalogue: MovementCatalogue([]), asOf: Self.moment(4, 7)).readiness)

        #expect(readiness.score == 62)
        #expect(readiness.band == "Pay attention")
        #expect(readiness.asksForAttention)
    }

    // MARK: - The morning wake

    @Test("The wake asked for is the next morning, later today when the morning is still ahead")
    func theWakeIsTheNextMorning() {
        let evening = Self.moment(4, 21)
        let beforeDawn = Self.moment(5, 3)

        #expect(MorningRefresh.earliest(after: evening, calendar: Self.calendar) == Self.moment(5, 5, 30))
        #expect(MorningRefresh.earliest(after: beforeDawn, calendar: Self.calendar) == Self.moment(5, 5, 30))
        // Exactly at the hour is past it: the wake that ran then asks for tomorrow's.
        #expect(MorningRefresh.earliest(after: Self.moment(5, 5, 30), calendar: Self.calendar) == Self.moment(6, 5, 30))
    }
}
