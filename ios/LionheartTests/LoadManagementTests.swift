import Foundation
import Testing

@testable import Lionheart

/// The week's load as the client reads it: the advice the heroes carry, the injuries the week manages
/// around, everyday load in the going-in sheet, and the families a new injury is logged with.
@Suite("Load management")
struct LoadManagementTests {
    private static let monday = CalendarDay(year: 2026, month: 11, day: 9)

    @Test("The week's expectations carry its advice, its injuries and which families they pause")
    func expectationsDecodeTheirLoad() throws {
        let expectations = try Decoding.decode(WeekExpectations.self, Self.expectationsJSON)

        let advice = try #require(expectations.advice)
        #expect(advice == WeeklyAdvice(
            kind: .manageInjury, weekStarting: Self.monday, headline: "Hold push while the shoulder settles",
            reasons: ["Push volume rose 40 % in the 4 weeks before it."]))
        let injury = try #require(expectations.injuries.first)
        #expect((injury.name, injury.families, injury.lastPain) == ("the shoulder", [.push], 3))
        #expect(injury.leadUp == [FamilyTrend(family: .push, recent: 11.2, earlier: 8, change: 0.4)])
        #expect(expectations.families.map(\.isPaused) == [true])
        #expect(ExpectationLines.of(expectations).map(\.isPaused) == [false, true])
    }

    @Test("Advice of a kind this build does not know still shows its headline; advice with no headline is left out")
    func unknownAdviceStillShows() throws {
        let unknown = try Decoding.decode(
            WeeklyAdvice.self, #"{"$type":"sleepMore","weekStarting":"2026-11-09","headline":"Sleep more","reasons":[]}"#)
        let expectations = try Decoding.decode(
            WeekExpectations.self,
            Self.expectationsJSON.replacingOccurrences(of: #""headline":"Hold push while the shoulder settles""#, with: #""headline":"""#))

        #expect(unknown.kind == .unrecognised("sleepMore"))
        #expect(expectations.advice == nil)
    }

    @Test("Everyday load reads against his usual day, and as MET-minutes before there is one")
    func everydayWords() throws {
        let days = try Decoding.list(EverydayDay.self, """
            [{"day":"2026-11-08","metMinutes":1440,"standing":{"$type":"judged","usual":800,"high":1000,"band":"farAbove"}},
             {"day":"2026-11-09","metMinutes":512.4,"standing":{"$type":"unjudged"}}]
            """)

        #expect(LoadWords.everydayFigure(days[0]) == "1.8× your usual, far above")
        #expect(LoadWords.everydayLabel(days[0], today: Self.monday) == "Everyday load, yesterday")
        #expect(LoadWords.everydayFigure(days[1]) == "512 MET-min")
    }

    @Test("The going-in load part reads the four weeks before and the everyday days since yesterday")
    func goingInLoadDecodes() throws {
        let load = try Decoding.decode(GoingInState.LoadPart.self, """
            {"score":{"$type":"scored","score":90,"share":0.15},"hardSets":6,"hardSetsLast7":24,"hardSetsChronic":20.5,
             "everyday":[{"day":"2026-11-08","metMinutes":900,"standing":{"$type":"judged","usual":800,"high":1000,"band":"usual"}}]}
            """)

        #expect(load.hardSetsChronic == 20.5)
        #expect(load.everyday.map(\.day) == [CalendarDay(year: 2026, month: 11, day: 8)])
    }

    @Test("A lead-up too small to be a trend says nothing")
    func smallLeadUpIsQuiet() {
        #expect(LoadWords.leadUp(FamilyTrend(family: .legs, recent: 10.4, earlier: 10, change: 0.04)) == nil)
        #expect(LoadWords.leadUp(FamilyTrend(family: .legs, recent: 6, earlier: 10, change: -0.4))
            == "Legs down 40 % in the 4 weeks before: 6 hard sets a week against 10")
    }

    @Test("A new injury's families go out once each, in push, pull, legs order")
    func draftFamilies() throws {
        let draft = try InjuryDraft.make(
            name: "Left knee", startedOn: Self.monday, notes: "", families: [.legs, .push], today: Self.monday)

        #expect(draft.families == [.push, .legs])
    }

    private static let expectationsJSON = """
        {"window":{"from":"2026-11-06","to":"2026-11-12"},
         "sessions":{"done":2,"raw":{"target":3,"committed":3,"floor":3},"adjusted":{"target":3,"committed":3,"floor":3},"reasons":[]},
         "families":[{"family":"push","sets":{"done":4,"raw":{"target":6,"committed":8,"floor":4,"chronic":5},
           "adjusted":{"target":0,"committed":0,"floor":0,"chronic":5},"reasons":["Paused while the shoulder is managed."]},"paused":true}],
         "cardio":{"measure":{"$type":"unknown","reason":"noHeartRate"},"zones":[]},
         "priorities":[],
         "injuries":[{"injuryId":"99999999-9999-9999-9999-999999999999","name":"the shoulder","since":"2026-11-02",
           "families":["push"],"movements":[],"lastPain":3,"preceding":[{"family":"push","recent":11.2,"earlier":8,"change":0.4}]}],
         "advice":{"$type":"manageInjury","weekStarting":"2026-11-09","injury":"the shoulder","families":["push"],
           "headline":"Hold push while the shoulder settles","reasons":["Push volume rose 40 % in the 4 weeks before it."]}}
        """
}
