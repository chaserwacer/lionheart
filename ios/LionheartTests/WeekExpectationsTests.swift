import Foundation
import Testing

@testable import Lionheart

/// The week's expectations, the adjusted score and the non-training calendar as the client reads
/// them: what the wire decodes to, the rows Train lists, and what a tap on the calendar asks for.
@Suite("Week expectations")
struct WeekExpectationsTests {
    private static let tuesday = CalendarDay(year: 2026, month: 11, day: 3)
    private static let sunday = CalendarDay(year: 2026, month: 11, day: 1)
    private static let weeklyId = UUID(uuidString: "77777777-7777-7777-7777-777777777777")!
    private static let onceId = UUID(uuidString: "88888888-8888-8888-8888-888888888888")!

    // MARK: - Decoding

    @Test("A score carries its adjusted score, the days behind it and every expectation with its reasons")
    func scoreDecodesItsExpectations() throws {
        let score = try Decoding.decode(TrainingScore.self, Self.scoreJSON)

        #expect(score.adjusted == AdjustedScore(
            value: 61, outOf: 100, availableDays: 5,
            parts: [AdjustedPart(part: .lifting, points: 30.1, outOf: 35), AdjustedPart(part: .cardio, points: nil, outOf: 25)]))
        let expectations = try #require(score.expectations)
        #expect(expectations.availability.availableDays == 5)
        #expect(expectations.availability.unavailable.map(\.day) == [Self.sunday, Self.tuesday])
        let push = try #require(expectations.families.first)
        #expect((push.family, push.sets.done, push.sets.raw.target, push.sets.adjusted.target) == (.push, 4, 6, 5))
        #expect(push.sets.reasons == ["4-week average 5 hard sets, +10 %: 6.", "Lowered to 5: 2 of 7 days marked non-training (travel)."])
        #expect(push.sets.isLowered)
        #expect(expectations.cardio.zones.isEmpty)
        let aim = try #require(expectations.priorities.first)
        #expect(aim.aim == .hold(load: 200, reps: 5, unit: .pounds, estimatedOneRepMax: 233.3, onSystemLoad: false))
        guard case .pinned(let sessions) = aim.frequency else {
            Issue.record("Expected a pinned frequency")
            return
        }
        #expect((sessions.done, sessions.adjusted.target) == (1, 2))
        #expect(aim.reasons == ["Going-in rating 58, under your usual day: hold the weight."])
    }

    @Test("A score from a server without expectations still loads, with neither")
    func olderScoreLoads() throws {
        let score = try Decoding.decode(
            TrainingScore.self,
            #"{"window":{"from":"2026-10-28","to":"2026-11-03"},"value":49,"outOf":100,"unknown":[]}"#)

        #expect(score.adjusted == nil)
        #expect(score.expectations == nil)
    }

    @Test("An aim, a frequency, a day or a schedule this build does not know still lets its list load")
    func unknownCasesStillLoad() throws {
        let aim = try Decoding.decode(
            PriorityAim.self,
            #"{"family":"push","movementDataId":"66666666-6666-6666-6666-666666666666","name":"Bench","aim":{"$type":"wave"},"frequency":{"$type":"daily"},"reasons":[]}"#)
        let day = try Decoding.decode(DayAvailability.self, #"{"$type":"holiday","day":"2026-11-03"}"#)
        let marked = try Decoding.list(
            NonTrainingDay.self,
            #"[{"nonTrainingDayId":"77777777-7777-7777-7777-777777777777","schedule":{"$type":"monthly","from":"2026-11-01"},"note":""}]"#)

        #expect((aim.aim, aim.frequency) == (.unrecognised("wave"), .unrecognised("daily")))
        #expect(day == .unrecognised(Self.tuesday, "holiday"))
        #expect(marked.map(\.schedule) == [.unrecognised("monthly")])
    }

    @Test("A day trained decodes as trained and counts as available")
    func trainedDayDecodes() throws {
        let day = try Decoding.decode(DayAvailability.self, #"{"$type":"trained","day":"2026-11-03"}"#)

        #expect(day == .trained(Self.tuesday))
        #expect(!day.isNonTraining)
    }

    @Test("Each of the three schedules decodes with its days")
    func schedulesDecode() throws {
        let marked = try Decoding.list(NonTrainingDay.self, #"""
        [{"nonTrainingDayId":"88888888-8888-8888-8888-888888888888","schedule":{"$type":"oneDay","day":"2026-11-03"},"note":"travel"},
         {"nonTrainingDayId":"77777777-7777-7777-7777-777777777777","schedule":{"$type":"everyWeek","from":"2026-11-01"},"note":""},
         {"nonTrainingDayId":"99999999-9999-9999-9999-999999999999","schedule":{"$type":"everyWeekUntil","from":"2026-11-01","until":"2026-12-06"},"note":""}]
        """#)

        #expect(marked.map(\.schedule) == [
            .oneDay(Self.tuesday),
            .everyWeek(from: Self.sunday),
            .everyWeekUntil(from: Self.sunday, until: CalendarDay(year: 2026, month: 12, day: 6)),
        ])
        #expect(marked.map(\.schedule.repeats) == [false, true, true])
        #expect(marked[0].note == "travel")
    }

    // MARK: - Train's rows

    @Test("The rows read sessions, then each family, then each band, and a row is met only when the week asks something")
    func linesReadInTheScoresOrder() throws {
        let expectations = try #require(try Decoding.decode(TrainingScore.self, Self.scoreJSON).expectations)

        let lines = ExpectationLines.of(expectations)

        #expect(lines.map(\.id) == ["sessions", "sets.push"])
        #expect(lines.map(\.isMet) == [true, false])
        let nothingAsked = ExpectationLine(
            measure: .sessions,
            expectation: Expectation(
                done: 0,
                raw: WeeklyTarget(target: 3, committed: 3, floor: 3, chronic: nil),
                adjusted: WeeklyTarget(target: 0, committed: 0, floor: 0, chronic: nil),
                reasons: []))
        #expect(!nothingAsked.isMet)
    }

    @Test("The days line names the days off and their notes once, or that every day was available")
    func weekAvailability() throws {
        let expectations = try #require(try Decoding.decode(TrainingScore.self, Self.scoreJSON).expectations)

        #expect(WeekAvailability.of(expectations.availability)
            == .short(available: 5, of: 7, off: [Self.sunday, Self.tuesday], notes: ["travel"]))
        #expect(WeekAvailability.of(AvailabilityCalendar(
            range: DayRange(from: Self.sunday, to: Self.tuesday), days: [.open(Self.sunday)], availableDays: 1)) == .everyDay)
    }

    // MARK: - The calendar

    private static var calendar: AvailabilityCalendar {
        AvailabilityCalendar(
            range: DayRange(from: sunday, to: tuesday),
            days: [
                .nonTraining(sunday, nonTrainingDayId: weeklyId, note: "", repeats: true),
                .planned(sunday.adding(days: 1)),
                .nonTraining(tuesday, nonTrainingDayId: onceId, note: "travel", repeats: false),
            ],
            availableDays: 1)
    }

    @Test("The calendar shows the non-training days selected, and a planned day is not one")
    func markedDays() {
        #expect(NonTrainingMarking.marked(in: Self.calendar) == [Self.sunday, Self.tuesday])
    }

    @Test("A day newly selected is marked once")
    func tapMarks() {
        let before = NonTrainingMarking.marked(in: Self.calendar)
        let monday = Self.sunday.adding(days: 1)

        #expect(NonTrainingMarking.change(from: before, to: before.union([monday]), in: Self.calendar) == .mark(monday))
    }

    @Test("Deselecting a day names the statement covering it, and whether it repeats")
    func tapUnmarks() {
        let before = NonTrainingMarking.marked(in: Self.calendar)

        #expect(NonTrainingMarking.change(from: before, to: [Self.sunday], in: Self.calendar)
            == .unmark(Self.tuesday, nonTrainingDayId: Self.onceId, repeats: false))
        #expect(NonTrainingMarking.change(from: before, to: [Self.tuesday], in: Self.calendar)
            == .unmark(Self.sunday, nonTrainingDayId: Self.weeklyId, repeats: true))
    }

    @Test("No change, or more than one at once, asks for nothing")
    func ambiguousSelectionsAskNothing() {
        let before = NonTrainingMarking.marked(in: Self.calendar)

        #expect(NonTrainingMarking.change(from: before, to: before, in: Self.calendar) == nil)
        #expect(NonTrainingMarking.change(from: before, to: [], in: Self.calendar) == nil)
    }

    // MARK: - Fixture

    private static var scoreJSON: String {
        #"""
        {"window":{"from":"2026-10-28","to":"2026-11-03"},"value":49,"outOf":100,"unknown":[],
         "adjusted":{"value":61,"outOf":100,"availableDays":5,"parts":[
            {"part":"lifting","points":30.1,"outOf":35},{"part":"cardio","points":null,"outOf":25}]},
         "expectations":{
          "window":{"from":"2026-10-28","to":"2026-11-03"},
          "availability":{"range":{"from":"2026-10-28","to":"2026-11-03"},"availableDays":5,"days":[
            {"$type":"open","day":"2026-10-28"},{"$type":"open","day":"2026-10-29"},{"$type":"planned","day":"2026-10-30"},
            {"$type":"open","day":"2026-10-31"},
            {"$type":"nonTraining","day":"2026-11-01","nonTrainingDayId":"77777777-7777-7777-7777-777777777777","note":"travel","repeats":false},
            {"$type":"open","day":"2026-11-02"},
            {"$type":"nonTraining","day":"2026-11-03","nonTrainingDayId":"88888888-8888-8888-8888-888888888888","note":"travel","repeats":false}]},
          "sessions":{"done":3,"raw":{"target":3,"committed":3,"floor":3,"chronic":null},
            "adjusted":{"target":3,"committed":3,"floor":3,"chronic":null},"reasons":["Committed 3 sessions a week."]},
          "families":[{"family":"push","lastTrained":"2026-11-02","sets":{"done":4,
            "raw":{"target":6,"committed":8,"floor":4,"chronic":5},"adjusted":{"target":5,"committed":6,"floor":3,"chronic":5},
            "reasons":["4-week average 5 hard sets, +10 %: 6.","Lowered to 5: 2 of 7 days marked non-training (travel)."]}}],
          "cardio":{"measure":{"$type":"unknown","reason":"noHeartRate"},"zones":[]},
          "priorities":[{"family":"push","movementDataId":"66666666-6666-6666-6666-666666666666","name":"Bench Press",
            "aim":{"$type":"hold","load":200,"reps":5,"unit":1,"estimatedOneRepMax":233.3,"onSystemLoad":false},
            "frequency":{"$type":"pinned","sessions":{"done":1,"raw":{"target":2,"committed":2,"floor":2,"chronic":null},
              "adjusted":{"target":2,"committed":2,"floor":2,"chronic":null},"reasons":["Pinned for 2 sessions a week."]}},
            "reasons":["Going-in rating 58, under your usual day: hold the weight."]}]}}
        """#
    }
}
