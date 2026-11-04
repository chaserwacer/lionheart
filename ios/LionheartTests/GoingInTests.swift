import Foundation
import Testing

@testable import Lionheart

/// The going-in state as the server sends it, what is next, and what the header makes of both for
/// the workout on screen.
@Suite("Going in")
struct GoingInTests {
    static let knee = "66666666-6666-6666-6666-666666666666"

    /// A state the way `GET /api/training/going-in` writes one: readiness and load scored, the night
    /// not synced, one injury naming the bench.
    static let state = """
        {"asOf":"2026-03-10T18:30:00-05:00",
         "rating":{"$type":"rated","score":79,"band":"normal"},
         "readiness":{"score":{"$type":"scored","score":77,"share":0.6667},"ouraScore":77,"usualLow":68,
           "usualHigh":86,"hrvBalance":85,"restingHeartRate":68,"temperatureDeviation":-0.11},
         "autonomic":{"score":{"$type":"notScored"},"hrv":null,"hrvBaseline":70.5,"lowestHeartRate":null,
           "lowestHeartRateBaseline":45.2,"baselineNights":40},
         "sleep":{"score":{"$type":"notScored"},"lastNight":null,
           "nightBefore":{"day":"2026-03-09","totalSleepSeconds":25200,"efficiency":88,"sleepScore":73}},
         "load":{"score":{"$type":"scored","score":64,"share":0.3333},"hardSets":16,"highIntensityMinutes":null,
           "zoneTwoMinutes":null,"hardSetsLast7":20,"hardSetsWeeklyMean28":14.25},
         "wellness":{"score":{"$type":"notScored"},"today":null},
         "injuries":[{"injuryId":"\(knee)","name":"Knee","lastPain":3,
           "movementDataIds":["\(AWorkout.squat.uuidString)"]}],
         "plannedToday":[{"movements":["Squat","Bench Press"]}],
         "recentMuscles":[{"muscle":"quads","lastTrained":"2026-03-09"},{"muscle":"newMuscle","lastTrained":"2026-03-09"}]}
        """

    /// What is next, in the science stream's shape: lift first, push due, its priority to beat.
    static let next = """
        {"day":"2026-03-10",
         "first":{"session":"lift","reasons":["Push is the longest since trained."]},
         "lift":{"$type":"train","families":["push"],"lastTrained":"2026-03-06",
           "priorities":[{"family":"push","movementDataId":"\(AWorkout.bench.uuidString)","name":"Bench Press",
             "aim":{"$type":"beat","load":185,"reps":5,"unit":1,"estimatedOneRepMax":215.8,"bestToBeat":214,
               "onSystemLoad":false}}],
           "reasons":[]},
         "cardio":{"$type":"zone2","minutes":45,"reasons":[]},
         "recovery":{"readiness":{"$type":"usual","ouraScore":77},"sleepMinutes":420}}
        """

    @Test("A state decodes whole: a part not scored is a case, and an unknown muscle still decodes")
    func decodesTheServersShape() throws {
        let state = try Decoding.decode(GoingInState.self, Self.state)

        #expect(state.rating == .rated(score: 79, band: .normal))
        #expect(state.readiness.score == .scored(score: 77, share: 0.6667))
        #expect(state.autonomic.score == .notScored)
        #expect(state.sleep.lastNight == nil)
        #expect(state.sleep.nightBefore?.day == CalendarDay(year: 2026, month: 3, day: 9))
        #expect(state.load.highIntensityMinutes == nil)
        #expect(state.injuries.first?.movementDataIds == [AWorkout.squat])
        #expect(state.plannedToday == [["Squat", "Bench Press"]])
        #expect(state.recentMuscles.map(\.muscle) == [.quads, .unrecognised("newMuscle")])
    }

    @Test("A rating case this build does not know is unrecognised, not a failure")
    func anUnknownRatingIsUnrecognised() throws {
        let json = Self.state.replacingOccurrences(
            of: #"{"$type":"rated","score":79,"band":"normal"}"#, with: #"{"$type":"provisional"}"#)
        #expect(try Decoding.decode(GoingInState.self, json).rating == .unrecognised("provisional"))
    }

    @Test("A rating or a part score with no number is unrecognised, never read as nought")
    func aMissingScoreIsNotZero() throws {
        let json = Self.state
            .replacingOccurrences(of: #""score":79,"band":"normal""#, with: #""band":"normal""#)
            .replacingOccurrences(of: #"{"$type":"scored","score":64,"share":0.3333}"#, with: #"{"$type":"scored"}"#)
        let state = try Decoding.decode(GoingInState.self, json)

        #expect(state.rating == .unrecognised("rated"))
        #expect(state.load.score == .unrecognised("scored"))
    }

    @Test("What is next decodes with the aim's weight in its own numeric unit")
    func decodesWhatIsNext() throws {
        let next = try Decoding.decode(NextSessions.self, Self.next)

        #expect(next.first.session == .lift)
        guard case .train(let families, _, let priorities, _) = next.lift else {
            Issue.record("Expected a lift to train")
            return
        }
        #expect(families == [.push])
        #expect(priorities.first?.aim == .beat(
            load: 185, reps: 5, unit: .pounds, estimatedOneRepMax: 215.8, bestToBeat: 214, onSystemLoad: false))
        #expect(next.cardio == .zone2(minutes: 45, reasons: []))
        #expect(next.recovery.readiness == .usual(OuraScore(77)))
    }

    @Test("An injury touching a movement in this workout comes first and marks that movement")
    func injuriesTouchingTheWorkoutLead() throws {
        let state = try Decoding.decode(GoingInState.self, Self.state)
        let elsewhere = GoingInState.InjuryNote(id: UUID(), name: "Wrist", lastPain: nil, movementDataIds: [])
        let both = GoingInState(
            asOf: state.asOf, rating: state.rating, readiness: state.readiness, autonomic: state.autonomic,
            sleep: state.sleep, load: state.load, wellness: state.wellness, injuries: [elsewhere] + state.injuries,
            plannedToday: [], recentMuscles: [])

        let reading = GoingInReading.of(both, next: nil, movements: [AWorkout.bench, AWorkout.squat], hasWork: false)

        #expect(reading.injuries.map(\.note.name) == ["Knee", "Wrist"])
        #expect(reading.injuries.first?.touching == [AWorkout.squat])
        #expect(reading.injuries(touching: AWorkout.squat).map(\.name) == ["Knee"])
        #expect(reading.injuries(touching: AWorkout.bench).isEmpty)
    }

    @Test("The header is full until the first set of work, and names the part holding it down")
    func expandedUntilWorkAndWeakestPart() throws {
        let state = try Decoding.decode(GoingInState.self, Self.state)

        let before = GoingInReading.of(state, next: nil, movements: [], hasWork: false)
        let after = GoingInReading.of(state, next: nil, movements: [], hasWork: true)

        #expect(before.isExpanded)
        #expect(!after.isExpanded)
        #expect(before.weakest?.kind == .load)
        #expect(before.unread == [.autonomic, .sleep, .wellness])
    }

    @Test("A due priority already in the workout is marked so")
    func priorityInTheWorkoutIsMarked() throws {
        let state = try Decoding.decode(GoingInState.self, Self.state)
        let next = try Decoding.decode(NextSessions.self, Self.next)

        let reading = GoingInReading.of(state, next: next, movements: [AWorkout.bench], hasWork: false)

        #expect(reading.upNext?.first == .lift)
        #expect(reading.upNext?.priorities.map(\.isInWorkout) == [true])
    }

    @Test("Nothing logged to read an order from gives no up-next line")
    func nothingLoggedGivesNoLine() throws {
        let state = try Decoding.decode(GoingInState.self, Self.state)
        let next = try Decoding.decode(NextSessions.self, Self.next.replacingOccurrences(
            of: #""lift":{"$type":"train""#, with: #""lift":{"$type":"nothingLogged","reasons":[]},"unused":{"$type":"train""#))

        #expect(GoingInReading.of(state, next: next, movements: [], hasWork: false).upNext == nil)
    }
}
