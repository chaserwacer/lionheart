import Foundation
import Testing

@testable import Lionheart

/// Records as values, for the derivations: each takes only what a test varies.
enum ARecord {
    static let bench = UUID(uuidString: "22222222-2222-2222-2222-222222222222")!
    static let day: TimeInterval = 86_400
    static let start = Date(timeIntervalSince1970: 1_790_000_000)

    static func record(
        _ achievement: Achievement,
        on subject: RecordSubject = .movement(bench),
        dayOffset: Double = 0,
        previousDayOffset: Double? = nil,
        standing: PersonalRecord.Standing = .standing
    ) -> PersonalRecord {
        PersonalRecord(
            personalRecordId: UUID(),
            subject: subject,
            achievement: achievement,
            achievedAt: start.addingTimeInterval(dayOffset * day),
            lineage: previousDayOffset.map {
                .replaced(previousAchievedAt: start.addingTimeInterval($0 * day))
            } ?? .firstOfItsKind,
            standing: standing)
    }

    static func lift(_ weight: Double, _ reps: Int, _ unit: WeightUnit = .pounds) -> Achievement.Lift {
        Achievement.Lift(
            weight: weight, unit: unit, reps: reps,
            estimatedOneRepMax: reps <= 1 ? weight : weight * (1 + Double(reps) / 30))
    }

    static func heaviest(_ weight: Double, _ reps: Int, _ unit: WeightUnit = .pounds, dayOffset: Double = 0) -> PersonalRecord {
        record(.heaviestSet(lift(weight, reps, unit)), dayOffset: dayOffset)
    }

    /// A rep max at `reps`, held by a set of `setReps` — the same count unless it was carried down
    /// from a longer set.
    static func repMax(
        _ weight: Double, _ reps: Int, _ unit: WeightUnit = .pounds, setReps: Int? = nil, dayOffset: Double = 0
    ) -> PersonalRecord {
        record(.repMax(reps: reps, lift(weight, setReps ?? reps, unit)), dayOffset: dayOffset)
    }

    static func setVolume(_ weight: Double, _ reps: Int, _ unit: WeightUnit = .pounds) -> PersonalRecord {
        record(.bestSetVolume(Achievement.SetVolume(
            volume: weight * Double(reps), unit: unit, weight: weight, reps: reps)))
    }

    static func held(_ records: [PersonalRecord], on subject: RecordSubject = .movement(bench)) -> SubjectRecords {
        SubjectRecords(subject: subject, records: records, lastAchievedAt: records.map(\.achievedAt).max())
    }
}

// MARK: - Decoding

/// The wire, as the server's `PersonalRecordView` and `SubjectRecordsView` actually send it.
@Suite("Personal record decoding")
struct PersonalRecordDecodingTests {
    private static let bench = "22222222-2222-2222-2222-222222222222"

    private static func record(
        _ achievement: String,
        movementId: String? = bench,
        previous: String = "null",
        standing: String = "Standing"
    ) -> String {
        let movement = movementId.map { "\"\($0)\"" } ?? "null"
        return """
            {"personalRecordId":"01a19f63-244a-725f-b614-d907bb99d93e","movementDataId":\(movement),
             "achievement":\(achievement),"achievedAt":"2026-10-19T23:30:00",
             "previousAchievedAt":\(previous),"standing":"\(standing)"}
            """
    }

    @Test("Each of the seven achievements decodes to its own case, carrying the server's figures")
    func everyKindDecodes() throws {
        let achievements = try [
            #"{"$type":"heaviestSet","weight":245,"unit":1,"reps":1,"estimatedOneRepMax":245}"#,
            #"{"$type":"bestSetVolume","volume":2025,"unit":1,"weight":135,"reps":15}"#,
            #"{"$type":"repMax","reps":5,"weight":205,"unit":1,"estimatedOneRepMax":239.16666666666669}"#,
            #"{"$type":"bestSessionVolume","volume":10830,"unit":1}"#,
            #"{"$type":"longestDistance","distance":13.1,"unit":2,"duration":"02:04:30"}"#,
            #"{"$type":"fastestTime","distanceMeters":5000,"distance":5,"unit":3,"duration":"00:26:45"}"#,
            #"{"$type":"bestPace","pacePerKilometer":"00:05:21","pacePerMile":"00:08:36.5994240","distance":5,"unit":3,"duration":"00:26:45"}"#,
        ].map { try Decoding.decode(PersonalRecord.self, Self.record($0)).achievement }

        #expect(achievements[0] == .heaviestSet(.init(weight: 245, unit: .pounds, reps: 1, estimatedOneRepMax: 245)))
        #expect(achievements[1] == .bestSetVolume(.init(volume: 2025, unit: .pounds, weight: 135, reps: 15)))
        // The estimate is the server's, received to the digit rather than recomputed here.
        #expect(achievements[2] == .repMax(reps: 5, .init(weight: 205, unit: .pounds, reps: 5, estimatedOneRepMax: 239.16666666666669)))
        #expect(achievements[3] == .bestSessionVolume(.init(volume: 10830, unit: .pounds)))
        #expect(achievements[4] == .longestDistance(.init(distance: 13.1, unit: .miles, duration: 7470)))
        #expect(achievements[5] == .fastestTime(meters: 5000, .init(distance: 5, unit: .kilometers, duration: 1605)))
        guard case .bestPace(let pace) = achievements[6] else {
            Issue.record("Expected a pace")
            return
        }
        #expect(pace.perKilometer == 321)
        // A TimeSpan with seven fractional digits — what a pace per mile actually serializes as.
        #expect(abs(pace.perMile - 516.599424) < 0.000_001)
    }

    @Test("Each kind names its own identity, the bracket included")
    func kindsCarryTheirBracket() throws {
        let repMax = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"repMax","reps":5,"weight":205,"unit":1,"estimatedOneRepMax":239}"#))
        let fastest = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"fastestTime","distanceMeters":21082,"distance":13.1,"unit":2,"duration":"02:04:30"}"#,
            movementId: nil))

        #expect(repMax.key == RecordKey(subject: .movement(ARecord.bench), kind: .repMax(reps: 5), heldIn: .pounds))
        #expect(fastest.key == RecordKey(subject: .trailRunning, kind: .fastestTime(meters: 21082), heldIn: nil))
        #expect(repMax.achievement.kind.bracket == 5)
        #expect(RecordKind.heaviestSet.bracket == nil)
    }

    @Test("A rep max carried down from a longer set keeps its rep count and the set's apart")
    func aCarriedRepMaxKeepsBothCounts() throws {
        let six = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"repMax","reps":6,"weight":180,"unit":0,"estimatedOneRepMax":222,"setReps":7}"#))

        // Filed under six — the record's identity — while the set it came from is the seven.
        #expect(six.key.kind == .repMax(reps: 6))
        #expect(six.achievement == .repMax(reps: 6, .init(weight: 180, unit: .kilograms, reps: 7, estimatedOneRepMax: 222)))
    }

    @Test("An absent movement is trail running, not an unknown movement")
    func anAbsentMovementIsTrailRunning() throws {
        let record = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"longestDistance","distance":10,"unit":3,"duration":"00:56:40"}"#, movementId: nil))

        #expect(record.subject == .trailRunning)
        #expect(record.subject.movementDataId == nil)
    }

    @Test("An achievement this build does not know still decodes, as one it cannot show")
    func anUnknownAchievementIsKept() throws {
        let record = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"longestPlank","seconds":300}"#))

        #expect(record.achievement == .unrecognised("longestPlank"))
        #expect(record.achievement.kind.hasHistory == false)
        #expect(record.achievement.heldIn == nil)
    }

    @Test("Standing is one of three strings, and a fourth is kept rather than guessed at")
    func standingDecodesTotally() throws {
        let beaten = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"bestSessionVolume","volume":1,"unit":0}"#, standing: "Beaten"))
        let retracted = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"bestSessionVolume","volume":1,"unit":0}"#, standing: "Retracted"))
        let unknown = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"bestSessionVolume","volume":1,"unit":0}"#, standing: "Disputed"))

        #expect(beaten.standing == .beaten)
        #expect(retracted.standing == .retracted)
        #expect(unknown.standing == .unrecognised("Disputed"))
        // Only the one that stands can be withdrawn; an unfamiliar standing is not offered.
        #expect(!beaten.isRetractable)
        #expect(!unknown.isRetractable)
    }

    @Test("A first record has no predecessor, which is not a predecessor of zero days")
    func lineageIsTwoCases() throws {
        let first = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"heaviestSet","weight":1,"unit":0,"reps":1,"estimatedOneRepMax":1}"#))
        let replaced = try Decoding.decode(PersonalRecord.self, Self.record(
            #"{"$type":"heaviestSet","weight":1,"unit":0,"reps":1,"estimatedOneRepMax":1}"#,
            previous: #""2026-09-22T23:30:00""#))

        #expect(first.lineage == .firstOfItsKind)
        #expect(first.daysPredecessorStood == nil)
        #expect(replaced.daysPredecessorStood == 27)
    }

    @Test("One malformed record costs itself, not the movement's other records")
    func oneBadRecordDoesNotEmptyTheList() throws {
        let summaries = try Decoding.list(SubjectRecords.self, """
            [{"movementDataId":"\(Self.bench)","lastAchievedAt":"2026-10-19T23:30:00","records":[
                \(Self.record(#"{"$type":"heaviestSet","weight":245,"unit":1,"reps":1,"estimatedOneRepMax":245}"#)),
                {"personalRecordId":"not-a-guid","achievement":{"$type":"repMax"}},
                \(Self.record(#"{"$type":"futureKind"}"#))
             ]},
             {"movementDataId":null,"lastAchievedAt":null,"records":[]}]
            """)

        #expect(summaries.count == 2)
        #expect(summaries[0].records.count == 2)
        #expect(summaries[1].subject == .trailRunning)
        #expect(summaries[1].isEmpty)
    }

    @Test("A retraction says whether the beaten record stands again or nothing does")
    func retractionOutcomesDecode() throws {
        let restored = try Decoding.decode(RetractionOutcome.self, """
            {"$type":"restored","record":\(Self.record(#"{"$type":"heaviestSet","weight":225,"unit":1,"reps":1,"estimatedOneRepMax":225}"#))}
            """)
        let nothing = try Decoding.decode(RetractionOutcome.self, #"{"$type":"nothingStands"}"#)
        let unknown = try Decoding.decode(RetractionOutcome.self, #"{"$type":"deferred"}"#)

        guard case .restored(let record) = restored else {
            Issue.record("Expected the beaten record back")
            return
        }
        #expect(record.achievement.kind == .heaviestSet)
        #expect(nothing == .nothingStands)
        #expect(unknown == .unrecognised("deferred"))
    }
}

// MARK: - Reading one subject

@Suite("Record reading")
struct RecordReadingTests {
    @Test("Two records of one kind in different units are both kept, each in its own unit")
    func unitsAreSeparateRecords() {
        let reading = RecordReading.of(ARecord.held([
            ARecord.heaviest(245, 1, .pounds, dayOffset: 10),
            ARecord.heaviest(105, 2, .kilograms, dayOffset: 20),
        ]))

        #expect(reading.lifting.map(\.unit) == [.kilograms, .pounds])
        #expect(reading.lifting[0].heaviestSet?.achievement.heldIn == .kilograms)
        #expect(reading.lifting[1].heaviestSet?.achievement.heldIn == .pounds)
        // The list's headline is the unit lifted in most recently — not the larger number.
        #expect(reading.headline?.achievement.heldIn == .kilograms)
    }

    @Test("The grid runs one to twelve in rep order, whatever order the records arrived in")
    func theGridIsInRepOrderWithGaps() {
        let grid = RecordReading.of(ARecord.held([
            ARecord.repMax(160, 11, dayOffset: 30),
            ARecord.heaviest(245, 1, dayOffset: 30),
            ARecord.repMax(205, 5, dayOffset: 30),
            ARecord.repMax(215, 3, dayOffset: 10),
        ])).lifting[0].grid

        #expect(grid.cells.map(\.reps) == Array(1...12))
        #expect(grid.cells.map(\.weight) == [245, nil, 215, nil, 205, nil, nil, nil, nil, nil, 160, nil])
        #expect(grid.holdsRepMaxes)
    }

    @Test("The first cell is the heaviest set, since every set contains a single")
    func theFirstCellIsTheHeaviestSet() {
        let heaviest = ARecord.heaviest(185, 6, .kilograms)

        let grid = RecordReading.of(ARecord.held([
            heaviest,
            ARecord.repMax(185, 6, .kilograms),
            ARecord.repMax(180, 7, .kilograms),
        ])).lifting[0].grid

        #expect(grid.cells[0].record == heaviest)
        // The single was the front of the set of six, and the grid says so.
        #expect(grid.cells[0].longerSetReps == 6)
    }

    @Test("A cell held by the front of a longer set says how long it was; a clean set does not")
    func aCarriedCellNamesItsSet() {
        let grid = RepMaxGrid.of(heaviestSet: nil, repMaxes: [
            ARecord.repMax(185, 5, .kilograms, setReps: 6),
            ARecord.repMax(185, 6, .kilograms),
        ])

        #expect(grid.cells[4].longerSetReps == 6)
        #expect(grid.cells[5].longerSetReps == nil)
    }

    @Test("The cells set in the grid's latest session are the newest, and only those")
    func theNewestCellsAreMarked() {
        let grid = RepMaxGrid.of(heaviestSet: ARecord.heaviest(215, 3, dayOffset: 10), repMaxes: [
            ARecord.repMax(205, 5, dayOffset: 30),
            ARecord.repMax(215, 3, dayOffset: 10),
        ])

        #expect(grid.cells.filter(\.isNewest).map(\.reps) == [5])
    }

    @Test("A movement holding only its heaviest set has no grid to show, not one cell beside eleven dashes")
    func legacyRecordsHaveNoGrid() {
        let reading = RecordReading.of(ARecord.held([
            ARecord.heaviest(0, 0, .kilograms),
            ARecord.setVolume(0, 0, .kilograms),
        ]))

        #expect(!reading.lifting[0].grid.holdsRepMaxes)
        // A weight-zero record is real data and is shown as it is, not filtered out.
        #expect(reading.lifting[0].heaviestSet != nil)
    }

    @Test("The estimated single is the best the server estimated, among this unit's records only")
    func theStrongestEstimateIsSelectedNotComputed() {
        // 225 × 2 implies more than the 230 single; the kilograms ten implies more than either
        // as a number, and is a different unit's.
        let reading = RecordReading.of(ARecord.held([
            ARecord.heaviest(230, 1, .pounds),
            ARecord.repMax(225, 2, .pounds),
            ARecord.repMax(150, 10, .kilograms),
        ]))
        let pounds = reading.lifting.first { $0.unit == .pounds }

        #expect(pounds?.strongestEstimate?.achievement == .repMax(reps: 2, ARecord.lift(225, 2)))
    }

    @Test("A single actually lifted is no estimate, so it is not offered as one")
    func aLiftedSingleIsNotAnEstimate() {
        let reading = RecordReading.of(ARecord.held([
            ARecord.heaviest(245, 1),
            ARecord.repMax(225, 2),
        ]))

        #expect(reading.lifting[0].strongestEstimate == nil)
    }

    @Test("Retracting a record warns of what follows: the beaten one back, or nothing at all")
    func theRetractionWarningFollowsTheLineage() {
        let replaced = ARecord.record(.heaviestSet(ARecord.lift(245, 1)), previousDayOffset: -10)
        let first = ARecord.record(.heaviestSet(ARecord.lift(245, 1)))

        #expect(replaced.retractionConfirmation.warning.contains("The record it beat stands again."))
        #expect(first.retractionConfirmation.warning.contains("nothing will stand in its place"))
        #expect(replaced.retractionConfirmation.proceed == "Retract record")
    }

    @Test("Fastest times read shortest distance first, under endurance rather than lifting")
    func fastestTimesAreByDistance() {
        func fastest(_ meters: Int) -> PersonalRecord {
            ARecord.record(
                .fastestTime(meters: meters, .init(distance: Double(meters) / 1000, unit: .kilometers, duration: 60)),
                on: .trailRunning)
        }
        let reading = RecordReading.of(ARecord.held([fastest(21082), fastest(5000), fastest(10000)], on: .trailRunning))

        #expect(reading.lifting.isEmpty)
        #expect(reading.endurance?.fastestTimes.map(\.achievement.kind) == [
            .fastestTime(meters: 5000), .fastestTime(meters: 10000), .fastestTime(meters: 21082),
        ])
    }
}

// MARK: - A chain

@Suite("Record history")
struct RecordHistoryTests {
    private static let poundsFive = RecordKey(subject: .movement(ARecord.bench), kind: .repMax(reps: 5), heldIn: .pounds)

    @Test("A chain is narrowed to the record's own unit — the route answers every unit")
    func theChainIsOneUnit() {
        let chain = [
            ARecord.record(.repMax(reps: 5, ARecord.lift(205, 5)), dayOffset: 30, previousDayOffset: 20),
            ARecord.record(.repMax(reps: 5, ARecord.lift(95, 5, .kilograms)), dayOffset: 25),
            ARecord.record(.repMax(reps: 5, ARecord.lift(200, 5)), dayOffset: 20, standing: .beaten),
        ]

        let history = RecordHistory.reading(chain, of: Self.poundsFive)

        #expect(history.links.map(\.achievement.heldIn) == [.pounds, .pounds])
        #expect(history.trend.map(\.value) == [200, 205])
    }

    @Test("A withdrawn record is listed but is no point on the line")
    func aRetractedLinkIsNotATrendPoint() {
        let chain = [
            ARecord.record(.repMax(reps: 5, ARecord.lift(300, 5)), dayOffset: 40, standing: .retracted),
            ARecord.record(.repMax(reps: 5, ARecord.lift(205, 5)), dayOffset: 30),
            ARecord.record(.repMax(reps: 5, ARecord.lift(200, 5)), dayOffset: 20, standing: .beaten),
        ]

        let history = RecordHistory.reading(chain, of: Self.poundsFive)

        #expect(history.links.count == 3)
        #expect(history.trend.map(\.value) == [200, 205])
    }

    @Test("A migrated chain whose standing record is older than its beaten ones draws no line")
    func anIncoherentChainHasNoLine() {
        // The shape of the owner's legacy squat: a weight-zero record standing, newer beaten ones
        // behind it. A line through them would show 33 kg as the record that stands.
        let key = RecordKey(subject: .movement(ARecord.bench), kind: .heaviestSet, heldIn: .kilograms)
        let chain = [
            ARecord.record(.heaviestSet(ARecord.lift(33, 0, .kilograms)), dayOffset: 0.003, previousDayOffset: 0, standing: .beaten),
            ARecord.record(.heaviestSet(ARecord.lift(5, 0, .kilograms)), dayOffset: 0.002, previousDayOffset: 0, standing: .beaten),
            ARecord.record(.heaviestSet(ARecord.lift(0, 0, .kilograms)), dayOffset: 0),
        ]

        let history = RecordHistory.reading(chain, of: key)

        #expect(history.links.count == 3)
        #expect(history.trend.isEmpty)
    }

    @Test("One point is not a trend")
    func onePointHasNoLine() {
        let history = RecordHistory.reading(
            [ARecord.record(.repMax(reps: 5, ARecord.lift(205, 5)))], of: Self.poundsFive)

        #expect(history.links.count == 1)
        #expect(history.trend.isEmpty)
    }

    @Test("A longest distance logged in two units has no line rather than a converted one")
    func mixedDistanceUnitsHaveNoLine() {
        let key = RecordKey(subject: .trailRunning, kind: .longestDistance, heldIn: nil)
        let chain = [
            ARecord.record(.longestDistance(.init(distance: 13.1, unit: .miles, duration: 7470)), on: .trailRunning, dayOffset: 30),
            ARecord.record(.longestDistance(.init(distance: 10, unit: .kilometers, duration: 3400)), on: .trailRunning, dayOffset: 20, standing: .beaten),
        ]

        let history = RecordHistory.reading(chain, of: key)

        #expect(history.links.count == 2)
        #expect(history.trend.isEmpty)
    }

    @Test("A standing record counts its days; a beaten one leaves that to the record that beat it")
    func daysStanding() {
        let now = ARecord.start.addingTimeInterval(14 * ARecord.day + 3600)

        #expect(ARecord.record(.repMax(reps: 5, ARecord.lift(205, 5))).daysStanding(asOf: now) == 14)
        #expect(ARecord.record(.repMax(reps: 5, ARecord.lift(205, 5)), standing: .beaten).daysStanding(asOf: now) == nil)
    }
}
