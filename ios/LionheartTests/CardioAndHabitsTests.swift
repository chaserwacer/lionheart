import Foundation
import Testing

@testable import Lionheart

/// The cardio and habits reports as the client reads them: what the wire decodes to, and the few
/// rules the screens apply to what the server computed.
@Suite("Cardio and habits")
struct CardioAndHabitsTests {
    // MARK: - Decoding the cardio report

    @Test("A cardio report decodes its maximum, a session's zones and the aerobic-base windows")
    func cardioReportDecodes() throws {
        let report = try Decoding.decode(CardioReport.self, Self.cardioJSON)

        #expect(report.maxHeartRate == .agePredicted(bpm: 194, age: 20, observedPeak: 152))
        #expect(report.maxHeartRate?.zoneFloors == [97, 117, 136, 156, 175])

        let session = try #require(report.sessions.first)
        #expect(session.activityName == "Cardiovascular exercise")
        guard case .recorded(let average, let peak, _, let zones?, _) = session.heartRate else {
            Issue.record("Expected a recorded heart rate, got \(session.heartRate)")
            return
        }
        #expect((average, peak) == (127, 141))
        #expect(zones.zone2 == 33)
        #expect(report.aerobicBase.zone2MinutesPerWeek.recent == nil)
        #expect(report.aerobicBase.vo2Max.change == 1)
        #expect(report.weeks.map(\.sessions) == [0, 1])
        #expect(report.weeks.map(\.daysInRange) == [6, 7])
        #expect(report.excluded.map(\.reason) == [.notTraining, .unrecognised("somethingNew")])
        #expect(session.source == .ringWorkout(
            ouraId: try #require(UUID(uuidString: "25e651b9-6f28-4091-8341-a7f2332a2fc8")),
            activity: "cardiovascularExercise", category: .endurance, entry: .entered))
    }

    @Test("A declined ring guess decodes with the heart rate that decided it, and opens like a session")
    func declinedDecodes() throws {
        let report = try Decoding.decode(CardioReport.self, Self.cardioJSON)

        let declined = try #require(report.declined.first)
        #expect(declined.reason == .heartRateBelowTraining)
        guard case .ringWorkout(_, "handball", .sport, .detected) = declined.session.source else {
            Issue.record("Expected a detected handball, got \(declined.session.source)")
            return
        }
        #expect(report.session(withId: declined.session.id) == declined.session)
        #expect(report.sessions.allSatisfy { $0.id != declined.session.id })
        #expect(CardioWords.verdict(declined.reason) == "below training")
    }

    @Test("Runs read as heartbeats per kilometre only with distance and most of their time read")
    func runReadings() throws {
        func run(_ id: String, metres: Double?, coverage: Double) -> CardioSession {
            let start = Date(timeIntervalSince1970: 1_790_000_000)
            return CardioSession(
                source: .loggedSession(trainingSessionId: try! #require(UUID(uuidString: id)), isTrailRun: true, isRecorded: true, activity: nil),
                start: start, end: start.addingTimeInterval(30 * 60), distanceMeters: metres,
                heartRate: .recorded(averageBpm: 150, peakBpm: 170, coverage: coverage, zones: nil, source: .appleHealth))
        }
        let base = try Decoding.decode(CardioReport.self, Self.cardioJSON)
        let report = CardioReport(
            range: base.range, maxHeartRate: base.maxHeartRate,
            sessions: [
                run("11111111-1111-1111-1111-111111111111", metres: 5000, coverage: 0.9),
                run("22222222-2222-2222-2222-222222222222", metres: nil, coverage: 0.9),
                run("33333333-3333-3333-3333-333333333333", metres: 5000, coverage: 0.3),
            ] + base.sessions,
            weeks: base.weeks, vo2Max: [], cardiovascularAge: [], restingHeartRate: [],
            aerobicBase: base.aerobicBase, excluded: [], declined: [])

        let readings = RunReading.of(report)

        let reading = try #require(readings.first)
        #expect(readings.count == 1)
        #expect(reading.secondsPerKilometre == 360)
        // 150 bpm for six minutes a kilometre.
        #expect(reading.beats(per: .kilometres) == 900)
        #expect(abs(reading.beats(per: .miles) - 900 * 1.609344) < 0.001)
    }

    @Test("The zone split is per week of days in the span, and zones 3–5 are zone 3 and up")
    func zoneSplit() throws {
        let report = try Decoding.decode(CardioReport.self, Self.cardioJSON)

        let split = ZoneSplit.of(report.weeks)

        // 13 days in the span: 33 minutes of zone 2 and 5 of zone 3 over 13/7 weeks.
        #expect(abs(split.zone2PerWeek - 33 * 7 / 13) < 0.001)
        #expect(abs(split.zones3To5PerWeek - 5 * 7 / 13) < 0.001)
        #expect(abs((split.hardShare ?? 0) - 5.0 / 38) < 0.001)
    }

    @Test("A session source or maximum the client does not know still lets the report load")
    func unknownSourcesDegrade() throws {
        let json = Self.cardioJSON
            .replacingOccurrences(of: "\"ringWorkout\"", with: "\"treadmillImport\"")
            .replacingOccurrences(of: "\"agePredicted\"", with: "\"labTested\"")

        let report = try Decoding.decode(CardioReport.self, json)

        #expect(report.maxHeartRate == .unrecognised("labTested"))
        #expect(report.sessions.first?.source == .unrecognised("treadmillImport"))
    }

    // MARK: - Reading the comparisons

    @Test("A change reads as better or worse by the figure's own polarity, and unknown when a window is empty")
    func aerobicBaseDirection() {
        let resting = AerobicBaseFigure(measure: .restingHeartRate, comparison: .init(recent: 43, before: 41))
        let zone2 = AerobicBaseFigure(measure: .zone2MinutesPerWeek, comparison: .init(recent: 90, before: 60))
        let vo2 = AerobicBaseFigure(measure: .vo2Max, comparison: .init(recent: 47, before: nil))

        #expect(resting.direction == .worse)
        #expect(zone2.direction == .better)
        #expect(vo2.direction == .unknown)
    }

    @Test("More sitting is the worse way; a later bedtime is not judged at all")
    func habitsPolarity() {
        let more = SpanComparison(recent: 95, before: 80)

        #expect(HabitsComparison.isWorse(more, HabitsComparison.Measure.longestSitting.polarity))
        #expect(!HabitsComparison.isWorse(more, HabitsComparison.Measure.steps.polarity))
        #expect(!HabitsComparison.isWorse(more, HabitsComparison.Measure.bedtime.polarity))
    }

    @Test("Social jet lag is the weekend's sleep midpoint against the weekdays'")
    func socialJetLag() {
        let summary = HabitsSummary(
            range: Self.range,
            weekdays: Self.figures(midpoint: 300),
            weekends: Self.figures(midpoint: 345),
            allDays: Self.figures(midpoint: 310))

        #expect(SocialJetLag.minutes(summary) == 45)
    }

    // MARK: - The day's shape

    @Test("A bedtime keeps the time it was on the athlete's own clock, whatever the phone's zone")
    func localClockReadsTheWireOffset() throws {
        let clock = try #require(LocalClock(wire: "2026-08-01T01:07:01-04:00"))

        #expect(clock.minutesAfterMidnight == 67)
        #expect(clock.minutesAfterNoon == 787)
    }

    @Test("The hour chart starts at 04:00, the way Oura's activity day does")
    func hourPositions() {
        #expect(HourSegment.position(of: 4) == 0)
        #expect(HourSegment.position(of: 23) == 19)
        #expect(HourSegment.position(of: 3) == 23)
    }

    @Test("A run inside a scored sleep is drawn as sleep, and non-wear is left as a gap")
    func activityLanes() {
        let start = Date(timeIntervalSince1970: 0)
        #expect(ActivityLane.of(ActivityRun(activityClass: .rest, start: start, minutes: 30, isAsleep: true)) == .asleep)
        #expect(ActivityLane.of(ActivityRun(activityClass: .rest, start: start, minutes: 30, isAsleep: false)) == .rest)
        #expect(ActivityLane.of(ActivityRun(activityClass: .nonWear, start: start, minutes: 30, isAsleep: false)) == nil)
    }

    @Test("A day's timeline decodes its runs, sittings and spans")
    func dayDetailDecodes() throws {
        let detail = try Decoding.decode(HabitsDayDetail.self, Self.dayJSON)

        #expect(detail.figures.longestSittingMinutes == 60)
        #expect(detail.runs.map(\.activityClass) == [.rest, .inactive, .high])
        #expect(detail.runs.first?.isAsleep == true)
        #expect(detail.sittings.map(\.minutes) == [60])
        #expect(detail.training.first.map(TrainingSpanName.of) == "Strength session")
        #expect(detail.sleep.first?.isMainSleep == true)
    }

    // MARK: - Oura's vocabulary

    @Test("Oura's camel-cased activities read as words, and its snake-cased vocabularies still do")
    func vocabularyPhrases() {
        #expect(OuraVocabulary.phrase("strengthTraining") == "Strength training")
        #expect(OuraVocabulary.phrase("houseWork") == "House work")
        #expect(OuraVocabulary.phrase("restored_day") == "Restored day")
        #expect(OuraVocabulary.phrase("running") == "Running")
    }

    // MARK: - Fixtures

    private static let range = DayRange(
        from: CalendarDay(year: 2026, month: 10, day: 1), to: CalendarDay(year: 2026, month: 10, day: 30))

    private static func figures(midpoint: Double) -> HabitsFigures {
        HabitsFigures(
            completeDays: 10, steps: 9000, activeMinutes: 400, sedentaryMinutes: 480,
            longestSittingMinutes: 80, longSittings: 1.5, hours: [],
            sleep: SleepTiming(
                nights: 10, bedtimeMinutesAfterNoon: 780, bedtimeSpreadMinutes: 60,
                wakeMinutesAfterMidnight: 560, wakeSpreadMinutes: 50, midpointMinutesAfterMidnight: midpoint))
    }

    private static let cardioJSON = """
        {"range":{"from":"2026-10-20","to":"2026-11-02","days":14},
         "maxHeartRate":{"$type":"agePredicted","age":20,"observedPeak":152,"bpm":194},
         "sessions":[{"source":{"$type":"ringWorkout","ouraId":"25e651b9-6f28-4091-8341-a7f2332a2fc8",
                                "activity":"cardiovascularExercise","category":"endurance","entry":"entered"},
                      "start":"2026-10-31T20:51:00-04:00","end":"2026-10-31T21:36:00-04:00","distanceMeters":null,
                      "heartRate":{"$type":"recorded","averageBpm":127,"peakBpm":141,"coverage":0.91,
                                   "zones":{"belowZones":0,"zone1":3,"zone2":33,"zone3":5,"zone4":0,"zone5":0,
                                            "inZones":41,"load":84}},
                      "minutes":45}],
         "weeks":[{"weekStarting":"2026-10-19","daysInRange":6,"sessions":0,"minutes":0,
                   "zones":{"belowZones":0,"zone1":0,"zone2":0,"zone3":0,"zone4":0,"zone5":0,"inZones":0,"load":0}},
                  {"weekStarting":"2026-10-26","daysInRange":7,"sessions":1,"minutes":45,
                   "zones":{"belowZones":0,"zone1":3,"zone2":33,"zone3":5,"zone4":0,"zone5":0,"inZones":41,"load":84}}],
         "vo2Max":[{"day":"2026-10-21","value":46}],"cardiovascularAge":[],"restingHeartRate":[],
         "aerobicBase":{"recent":{"from":"2026-10-06","to":"2026-11-02"},"before":{"from":"2026-08-11","to":"2026-10-05"},
                        "zone2MinutesPerWeek":{"recent":null,"before":null},
                        "cardioMinutesPerWeek":{"recent":154,"before":290},
                        "vo2Max":{"recent":47,"before":46},
                        "restingHeartRate":{"recent":42.3,"before":41},
                        "cardiovascularAge":{"recent":null,"before":null}},
         "excluded":[{"category":"incidental","reason":"notTraining","count":723},
                     {"category":"walk","reason":"somethingNew","count":2}],
         "declined":[{"reason":"heartRateBelowTraining",
                      "session":{"source":{"$type":"ringWorkout","ouraId":"77777777-7777-7777-7777-777777777777",
                                           "activity":"handball","category":"sport","entry":"detected"},
                                 "start":"2026-10-29T17:00:00-04:00","end":"2026-10-29T17:21:00-04:00","distanceMeters":null,
                                 "heartRate":{"$type":"recorded","averageBpm":104,"peakBpm":121,"coverage":0.96,
                                              "zones":{"belowZones":2,"zone1":17,"zone2":1,"zone3":0,"zone4":0,"zone5":0,
                                                       "inZones":18,"load":21}}}}]}
        """

    private static let dayJSON = """
        {"figures":{"day":"2026-07-30","isWeekend":false,"isComplete":true,"steps":9807,"activeMinutes":506,
                    "sedentaryMinutes":447,"longestSedentaryMinutes":60,"longSedentaryBouts":1,
                    "bedtime":"2026-07-30T01:10:00-04:00","wakeTime":"2026-07-30T10:01:00-04:00"},
         "startsAt":"2026-07-30T04:00:00-04:00","endsAt":"2026-07-31T04:00:00-04:00",
         "runs":[{"class":"rest","start":"2026-07-30T04:00:00-04:00","minutes":360,"isAsleep":true},
                 {"class":"inactive","start":"2026-07-30T10:00:00-04:00","minutes":60,"isAsleep":false},
                 {"class":"high","start":"2026-07-30T11:00:00-04:00","minutes":5,"isAsleep":false}],
         "sedentaryBouts":[{"start":"2026-07-30T10:00:00-04:00","minutes":60}],
         "sleep":[{"start":"2026-07-30T01:10:00-04:00","end":"2026-07-30T10:01:00-04:00","isMainSleep":true}],
         "training":[{"activity":"strengthTraining","start":"2026-07-30T20:05:00-04:00",
                      "end":"2026-07-30T20:49:00-04:00","isLogged":true}]}
        """
}
