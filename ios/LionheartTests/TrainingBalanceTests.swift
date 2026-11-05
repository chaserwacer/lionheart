import Foundation
import Testing

@testable import Lionheart

/// Weekly volume, the priorities and the training score as the client reads them: what the wire
/// decodes to, and the readings the screens make of what the server computed.
@Suite("Training balance")
struct TrainingBalanceTests {
    // MARK: - Decoding

    @Test("Weekly volume decodes its families, a pattern with no target, a muscle's half sets and what is up next")
    func volumeDecodes() throws {
        let volume = try Decoding.decode(TrainingVolume.self, Self.volumeJSON)

        #expect(volume.window == DayRange(from: CalendarDay(year: 2026, month: 10, day: 28), to: CalendarDay(year: 2026, month: 11, day: 3)))
        #expect(volume.family(.pull) == FamilyVolume(family: .pull, hardSets: 3, target: 8, lastTrained: CalendarDay(year: 2026, month: 10, day: 28), isPaused: false))
        #expect(volume.patterns.first { $0.pattern == .isolation }?.target == nil)
        #expect(volume.muscles.first { $0.muscle == .upperBack }?.sets == 1.5)
        #expect(volume.unplaced.map(\.name) == ["Cable Mystery"])
        #expect(volume.upNext == .train(
            families: [.push, .pull], lastTrained: CalendarDay(year: 2026, month: 10, day: 28),
            trainedToday: false, readiness: .low(OuraScore(58), usualLow: OuraScore(63), usualHigh: OuraScore(87))))
    }

    @Test("A pattern, a muscle or an up-next case this build does not know still lets the volume load")
    func volumeDegrades() throws {
        let json = Self.volumeJSON
            .replacingOccurrences(of: "\"horizontalPull\"", with: "\"diagonalPull\"")
            .replacingOccurrences(of: "\"upperBack\"", with: "\"serratus\"")
            .replacingOccurrences(of: "\"$type\":\"train\"", with: "\"$type\":\"rest\"")

        let volume = try Decoding.decode(TrainingVolume.self, json)

        #expect(volume.patterns.contains { $0.pattern == .unrecognised("diagonalPull") })
        #expect(volume.muscles.contains { $0.muscle == .unrecognised("serratus") })
        #expect(volume.upNext == .unrecognised("rest"))
    }

    @Test("The score decodes each part's measurements, and unknown cardio as unknown rather than zero")
    func scoreDecodes() throws {
        let score = try Decoding.decode(TrainingScore.self, Self.scoreJSON(cardio: Self.measuredCardio))

        #expect(score.value == 49)
        #expect(!score.isPartial)
        #expect(score.lifting.families.map(\.of.hardSets) == [4, 3, 7])
        #expect(score.lifting.families.map(\.points) == [8.25, 7.14, 10.91])
        #expect(score.cardio.measure == .measured(
            zone2Minutes: 67.1, qualityMinutes: 0, qualitySessions: 0, sessions: 5, heartRateDays: 7))
        #expect(score.cardio.zones.map(\.of.band) == [.zone2, .quality])
        #expect(score.lifting.families.map(\.of.target.target) == [5, 4, 8])
        #expect(score.lifting.families[1].of.target.chronic == 0)
        #expect(score.cardio.zones[0].of.target == WeeklyTarget(target: 60, committed: 150, floor: 60, chronic: nil))
        #expect(score.cardio.zones.map(\.outOf) == [15, 10])
        #expect(score.consistency.blocks.map(\.of.sessions) == [2, 2, 1, 0])
        #expect(score.consistency.blocks.first?.of.window.to == CalendarDay(year: 2026, month: 11, day: 3))

        let partial = try Decoding.decode(
            TrainingScore.self,
            Self.scoreJSON(cardio: #"{"points":null,"outOf":25,"measure":{"$type":"unknown","reason":"noHeartRate"}}"#,
                           unknown: #"["cardio"]"#))
        #expect(partial.cardio.points == nil)
        #expect(partial.cardio.measure == .unknown(.noHeartRate))
        #expect(partial.unknown == [.cardio])
        #expect((score.outOf, partial.outOf) == (100, 75))
        #expect(partial.cardio.zones.isEmpty)
    }

    @Test("The breakdown lays the parts end to end on 100 points, each measurement under its part")
    func breakdownLaysParts() throws {
        let parts = ScoreBreakdown.of(try Decoding.decode(TrainingScore.self, Self.scoreJSON(cardio: Self.measuredCardio)))

        #expect(parts.map(\.part) == [.lifting, .cardio, .progress, .consistency])
        #expect(parts.map(\.start) == [0, 35, 60, 80])
        let lifting = parts[0]
        #expect(zip(lifting.cells.map(\.start), [0, 11.67, 23.34]).allSatisfy { abs($0 - $1) < 0.0001 })
        #expect(lifting.cells.map(\.label) == ["Push", "Pull", "Legs"])
        #expect(lifting.summary == "Push 4/5 · Pull 3/4 · Legs 7/8")
        #expect(lifting.cells.map(\.basis) == [
            "4-week mean 4.2 + 10 % · committed 8",
            "4-week mean 0 · the floor 4 · committed 8",
            "4-week mean 9 · at the committed 8",
        ])
        #expect(parts[1].cells.map(\.start) == [35, 50])
        #expect(parts[1].cells.map(\.figure) == ["67 of 60 min", "0 of 10 min"])
        #expect(parts[1].cells[0].basis == "No 4 weeks to grow from · committed 150")
        #expect(parts[3].cells.map(\.label) == ["Last 7 days", "7–13 days ago", "14–20 days ago", "21–27 days ago"])
        #expect(parts[3].summary == "2 · 2 · 1 · 0 of 3 a week, newest first")
    }

    @Test("What is next decodes the lift with its aim, the cardio with its structure, and which first")
    func nextDecodes() throws {
        let json = #"""
        {"day":"2026-11-04","first":{"session":"cardio","reasons":["Cardio is further behind."]},
         "lift":{"$type":"train","families":["legs"],"lastTrained":"2026-10-30",
           "priorities":[{"family":"legs","movementDataId":"88888888-8888-8888-8888-888888888888","name":"Squat",
             "aim":{"$type":"beat","load":305,"reps":5,"unit":1,"estimatedOneRepMax":355.8,"bestToBeat":350,"onSystemLoad":false}},
             {"family":"legs","movementDataId":"99999999-9999-9999-9999-999999999999","name":"Lunge","aim":{"$type":"establish"}}],
           "reasons":["Legs last trained 5 days ago."]},
         "cardio":{"$type":"quality","bouts":4,"boutMinutes":4,"recoveryMinutes":3,"workMinutes":16,"reasons":["Quality 0 of 20 min."]},
         "recovery":{"readiness":{"$type":"usual","ouraScore":81},"sleepMinutes":452,"hrv":71,"usualHrv":68.4,"hrvSpread":7.9}}
        """#

        let next = try Decoding.decode(NextSessions.self, json)

        #expect(next.first == FirstChoice(session: .cardio, reasons: ["Cardio is further behind."]))
        guard case .train(let families, let last, let priorities, _) = next.lift else {
            Issue.record("Expected a lift to train")
            return
        }
        #expect(families == [.legs])
        #expect(last == CalendarDay(year: 2026, month: 10, day: 30))
        #expect(priorities.map(\.aim) == [
            .beat(load: 305, reps: 5, unit: .pounds, estimatedOneRepMax: 355.8, bestToBeat: 350, onSystemLoad: false),
            .establish,
        ])
        #expect(next.cardio == .quality(bouts: 4, boutMinutes: 4, recoveryMinutes: 3, reasons: ["Quality 0 of 20 min."]))
        #expect(next.recovery.sleepMinutes == 452)

        let unknown = try Decoding.decode(
            NextSessions.self,
            #"{"day":"2026-11-04","first":{"session":"rest","reasons":[]},"lift":{"$type":"plan"},"cardio":{"$type":"swim"}}"#)
        #expect((unknown.first.session, unknown.lift, unknown.cardio) == (.unrecognised("rest"), .unrecognised("plan"), .unrecognised("swim")))
    }

    @Test("Progress names each family's lift, so an untrained lift does not read as an untrained family")
    func breakdownProgressNamesTheLift() throws {
        let progress = #"""
        {"points":0,"outOf":20,"families":[
          {"of":{"family":"push","lift":{"$type":"chosen","movementDataId":"77777777-7777-7777-7777-777777777777",
            "name":"Barbell Overhead Press","choice":{"$type":"mostTrained","sessions":1},"progress":{"$type":"notTrained"},
            "sessionsLastFourWeeks":1,"series":[]}},"points":0,"outOf":6.67}]}
        """#
        let json = Self.scoreJSON(cardio: Self.measuredCardio)
            .replacingOccurrences(of: #""progress":{"points":13.3,"outOf":20,"families":[]}"#, with: #""progress":\#(progress)"#)
        let parts = ScoreBreakdown.of(try Decoding.decode(TrainingScore.self, json))

        #expect(parts[2].summary == "Push lift not trained this week")
    }

    @Test("Unknown cardio is one unknown cell across its part, never a zero")
    func breakdownUnknownCardio() throws {
        let partial = try Decoding.decode(
            TrainingScore.self,
            Self.scoreJSON(cardio: #"{"points":null,"outOf":25,"measure":{"$type":"unknown","reason":"noHeartRate"},"zones":[]}"#,
                           unknown: #"["cardio"]"#))

        let cardio = try #require(ScoreBreakdown.of(partial).first { $0.part == .cardio })

        #expect(cardio.points == nil)
        #expect(cardio.summary == "Unknown this week")
        let cell = try #require(cardio.cells.first)
        #expect((cardio.cells.count, cell.points, cell.outOf, cell.share) == (1, nil, 25, 0))
    }

    @Test("A week's points by part, its whole score by default, and nothing for a part it could not read")
    func weeklyPointsByPart() {
        let week = WeeklyScore(
            window: DayRange(from: CalendarDay(year: 2026, month: 10, day: 27), to: CalendarDay(year: 2026, month: 11, day: 2)),
            value: 51, outOf: 75, unknown: [.cardio],
            liftingPoints: 30, cardioPoints: nil, progressPoints: 6.7, consistencyPoints: 14)

        #expect(week.points(of: nil) == 51)
        #expect(week.points(of: .lifting) == 30)
        #expect(week.points(of: .cardio) == nil)
    }

    @Test("The trend context needs four weeks, and its best is the tallest week")
    func scoreTrend() {
        func week(_ value: Int, _ day: Int) -> WeeklyScore {
            WeeklyScore(
                window: DayRange(from: CalendarDay(year: 2026, month: 10, day: day - 6), to: CalendarDay(year: 2026, month: 10, day: day)),
                value: value, outOf: 100, unknown: [],
                liftingPoints: 0, cardioPoints: 0, progressPoints: 0, consistencyPoints: 0)
        }

        #expect(ScoreTrend.of([week(40, 7), week(60, 14), week(50, 21)]) == nil)
        #expect(ScoreTrend.of([week(40, 7), week(60, 14), week(50, 21), week(72, 28)])
                == ScoreTrend(median: 60, best: 72, weeks: 4))
    }

    @Test("A target counts as met at or past its figure, and only this week's can be")
    func targetsMet() throws {
        let score = try Decoding.decode(TrainingScore.self, Self.scoreJSON(cardio: Self.measuredCardio))
        // Zone 2 is past its 60; push, pull and legs are short of theirs, and so are the window's
        // sessions. A past block reaching three sessions is a week nothing now can move.
        let pastBlockMet = try Decoding.decode(
            TrainingScore.self,
            Self.scoreJSON(cardio: Self.measuredCardio)
                .replacingOccurrences(of: #""2026-10-27"},"sessions":2"#, with: #""2026-10-27"},"sessions":3"#))

        #expect(ScoreTargets.met(in: score) == ["cardio.Zone 2"])
        #expect(ScoreTargets.met(in: pastBlockMet) == ["cardio.Zone 2"])
    }

    @Test("A target just hit is the one met now that was not before")
    func targetJustHit() throws {
        let before = try Decoding.decode(TrainingScore.self, Self.scoreJSON(cardio: Self.measuredCardio))
        let after = try Decoding.decode(
            TrainingScore.self,
            Self.scoreJSON(cardio: Self.measuredCardio)
                .replacingOccurrences(of: #""family":"legs","hardSets":7"#, with: #""family":"legs","hardSets":8"#))

        #expect(ScoreTargets.newlyMet(from: before, to: after) == ["lifting.legs"])
        #expect(ScoreTargets.newlyMet(from: after, to: before).isEmpty)
        #expect(ScoreTargets.newlyMet(from: after, to: after).isEmpty)
    }

    @Test("Points read to one place where a third of a part is not whole")
    func scorePoints() {
        #expect(ScorePoints.text(35) == "35")
        #expect(ScorePoints.text(35.0 / 3) == "11.7")
        #expect(ScorePoints.earned(nil, of: 25) == "?/25")
    }

    @Test("A priority report decodes a pinned priority, a most-trained stand-in and a family with none")
    func prioritiesDecode() throws {
        let report = try Decoding.decode(PriorityReport.self, Self.prioritiesJSON)

        guard case .chosen(let push) = report.lift(for: .push), case .pinned(let pin) = push.choice else {
            Issue.record("Expected a pinned push priority, got \(report.lift(for: .push))")
            return
        }
        #expect(pin.role == .priority(.push))
        #expect(push.progress == .compared(best: 286, priorBest: 278, standing: .newBest, unit: .pounds, onSystemLoad: true))
        #expect(push.series.map(\.load) == [220, 225])
        guard case .chosen(let pull) = report.lift(for: .pull) else {
            Issue.record("Expected a pull priority")
            return
        }
        #expect(pull.choice == .mostTrained(sessions: 2))
        #expect(report.lift(for: .legs) == .none)
    }

    @Test("A movement's base classification and source decode off the movement")
    func movementClassificationDecodes() throws {
        let movement = try Decoding.decode(MovementData.self, Self.movementJSON)

        #expect(movement.movementBaseId == UUID(uuidString: "33333333-3333-3333-3333-333333333333"))
        #expect(movement.classification == .classified(
            MovementClassification(pattern: .horizontalPush, primary: [.chest, .triceps], secondary: [.frontDelts]),
            source: .mapUnsure(reason: "between two presses"), family: .push))
        #expect(movement.classification.family == .push)
    }

    @Test("A pin from a server older than roles reads as an emphasis")
    func pinWithNoRoleIsEmphasis() throws {
        let pin = try Decoding.decode(FocusPin.self, Self.pinJSON(role: nil))

        #expect(pin.role == .emphasis)
        #expect(try Decoding.decode(FocusPin.self, Self.pinJSON(role: #"{"$type":"priority","family":"legs"}"#)).role == .priority(.legs))
    }

    // MARK: - Readings

    @Test("The server's band names the verdict, and the change is read against the prior best")
    func liftStandingFromServer() {
        let best = LiftStanding.of(.compared(best: 210, priorBest: 200, standing: .newBest, unit: .pounds, onSystemLoad: false))
        let lifetime = LiftStanding.of(.compared(best: 260, priorBest: 200, standing: .lifetimeBest, unit: .pounds, onSystemLoad: false))
        let unknown = LiftStanding.of(.compared(best: 260, priorBest: 200, standing: .unrecognised("x"), unit: .pounds, onSystemLoad: false))

        guard case .newBest(_, _, let change, let isLifetime, _) = best else {
            Issue.record("Expected a new best, got \(best)")
            return
        }
        #expect(abs(change - 0.05) < 1e-9)
        #expect(!isLifetime)
        #expect(lifetime.verdict == "Lifetime best")
        #expect(best.verdict == "12-week best")
        #expect(unknown == .unreadable)
    }

    @Test("Consistency reads the window first, then each seven-day block before it")
    func consistencyBlocks() {
        let window = DayRange(from: CalendarDay(year: 2026, month: 10, day: 28), to: CalendarDay(year: 2026, month: 11, day: 3))
        let blocks = [2, 3, 0, 1].map { Scored(of: SessionBlock(window: window, sessions: $0), points: 0, outOf: 5) }

        let cells = ScoreBreakdown.consistency(ConsistencyPart(points: 0, outOf: 20, blocks: blocks, target: 3), from: 80)

        #expect(cells.map(\.label) == ["Last 7 days", "7–13 days ago", "14–20 days ago", "21–27 days ago"])
        #expect(cells.map(\.reading) == [2, 3, 0, 1].map { .quantity(measured: $0, target: 3, unit: .sessions) })
        #expect(cells.map(\.start) == [80, 85, 90, 95])
    }

    @Test("Marking a muscle in one role takes it out of the other, and marking it again clears it")
    func classificationDraftRoles() {
        var draft = ClassificationDraft(nil)
        #expect(draft.classification == nil)

        draft.toggle(.triceps, as: .secondary)
        draft.toggle(.triceps, as: .primary)
        draft.toggle(.chest, as: .primary)
        #expect(draft.classification?.primary == [.triceps, .chest])
        #expect(draft.classification?.secondary == [])

        draft.toggle(.triceps, as: .primary)
        #expect(draft.role(of: .triceps) == nil)
        #expect(draft.classification?.primary == [.chest])
    }

    @Test("A family's priority is chosen from its compound lifts and the unclassified ones, never its isolation work")
    func priorityCandidates() throws {
        let movements = try Decoding.list(MovementData.self, """
        [\(Self.movement(id: "1", base: "Bench Press", classification: Self.classified("horizontalPush", ["chest"]))),
         \(Self.movement(id: "2", base: "Lateral Raise", classification: Self.classified("isolation", ["sideDelts"]))),
         \(Self.movement(id: "3", base: "Pull Up", classification: Self.classified("verticalPull", ["lats"]))),
         \(Self.movement(id: "4", base: "Cable Mystery", classification: #"{"$type":"unclassified"}"#))]
        """)

        let push = PriorityCandidates.of(movements, for: .push)

        #expect(push.fitting.map(\.baseName) == ["Bench Press"])
        #expect(push.unclassified.map(\.baseName) == ["Cable Mystery"])
        #expect(PriorityCandidates.of(movements, for: .pull).fitting.map(\.baseName) == ["Pull Up"])
    }

    @Test("Families named together read as a list")
    func familiesListed() {
        #expect(PatternFamily.listed([.pull]) == "pull")
        #expect(PatternFamily.listed([.push, .pull]) == "push and pull")
        #expect(PatternFamily.listed([.push, .pull, .legs]) == "push, pull and legs")
    }

    // MARK: - Fixtures

    private static let volumeJSON = """
    {"window":{"from":"2026-10-28","to":"2026-11-03","days":7},
     "families":[{"family":"push","hardSets":4,"target":8,"lastTrained":"2026-10-28"},
                 {"family":"pull","hardSets":3,"target":8,"lastTrained":"2026-10-28"},
                 {"family":"legs","hardSets":7,"target":8,"lastTrained":"2026-11-02"}],
     "patterns":[{"pattern":"horizontalPull","hardSets":0,"target":4,"lastTrained":"2026-09-17"},
                 {"pattern":"isolation","hardSets":5,"target":null,"lastTrained":"2026-11-02"}],
     "muscles":[{"muscle":"upperBack","direct":0,"indirect":3,"target":4,"lastTrained":"2026-09-17","sets":1.5}],
     "unplaced":[{"movementDataId":"44444444-4444-4444-4444-444444444444","name":"Cable Mystery","hardSets":2}],
     "upNext":{"$type":"train","families":["push","pull"],"lastTrained":"2026-10-28","trainedToday":false,
               "readiness":{"$type":"low","ouraScore":58,"usualLow":63,"usualHigh":87}}}
    """

    private static let measuredCardio = """
    {"points":10,"outOf":25,"measure":{"$type":"measured","zone2Minutes":67.1,
     "qualityMinutes":0,"qualitySessions":0,"sessions":5,"heartRateDays":7},
     "zones":[{"of":{"band":"zone2","minutes":67.1,"target":{"target":60,"committed":150,"floor":60,"chronic":null}},"points":10,"outOf":15},
              {"of":{"band":"quality","minutes":0,"target":{"target":10,"committed":40,"floor":10,"chronic":0}},"points":0,"outOf":10}]}
    """

    private static func scoreJSON(cardio: String, unknown: String = "[]") -> String {
        """
        {"window":{"from":"2026-10-28","to":"2026-11-03"},"value":49,"outOf":\(unknown == "[]" ? 100 : 75),"unknown":\(unknown),
         "lifting":{"points":26.3,"outOf":35,"families":[
            {"of":{"family":"push","hardSets":4,"target":{"target":5,"committed":8,"floor":4,"chronic":4.2},"lastTrained":"2026-10-28"},"points":8.25,"outOf":11.67},
            {"of":{"family":"pull","hardSets":3,"target":{"target":4,"committed":8,"floor":4,"chronic":0},"lastTrained":"2026-10-28"},"points":7.14,"outOf":11.67},
            {"of":{"family":"legs","hardSets":7,"target":{"target":8,"committed":8,"floor":4,"chronic":9},"lastTrained":"2026-11-02"},"points":10.91,"outOf":11.67}]},
         "cardio":\(cardio),
         "progress":{"points":13.3,"outOf":20,"families":[]},
         "consistency":{"points":8.3,"outOf":20,"target":3,"blocks":[
            {"of":{"window":{"from":"2026-10-28","to":"2026-11-03"},"sessions":2},"points":4.1,"outOf":5},
            {"of":{"window":{"from":"2026-10-21","to":"2026-10-27"},"sessions":2},"points":4.1,"outOf":5},
            {"of":{"window":{"from":"2026-10-14","to":"2026-10-20"},"sessions":1},"points":2.9,"outOf":5},
            {"of":{"window":{"from":"2026-10-07","to":"2026-10-13"},"sessions":0},"points":0,"outOf":5}]}}
        """
    }

    private static func pinJSON(role: String?) -> String {
        let roleMember = role.map { #""role":\#($0),"# } ?? ""
        return """
        {"focusPinId":"55555555-5555-5555-5555-555555555555","movementDataId":"66666666-6666-6666-6666-666666666666",
         \(roleMember)"starts":"2026-11-02","ends":"2027-01-31","sessionsPerWeek":2,
         "target":{"$type":"none"},"status":{"$type":"pinned"},"pinnedAt":"2026-11-03T14:15:16Z"}
        """
    }

    private static var prioritiesJSON: String {
        """
        {"window":{"from":"2026-10-28","to":"2026-11-03"},
         "families":[
          {"family":"push","lift":{"$type":"chosen","movementDataId":"66666666-6666-6666-6666-666666666666","name":"Dip Bars Dip",
            "choice":{"$type":"pinned","pin":\(pinJSON(role: #"{"$type":"priority","family":"push"}"#))},
            "progress":{"$type":"compared","best":286,"priorBest":278,"allTimeBest":290,"standing":"newBest","unit":1,"onSystemLoad":true},
            "sessionsLastFourWeeks":3,
            "series":[{"day":"2026-10-21","estimatedOneRepMax":278,"load":220,"reps":8},
                      {"day":"2026-11-01","estimatedOneRepMax":286,"load":225,"reps":8}]}},
          {"family":"pull","lift":{"$type":"chosen","movementDataId":"77777777-7777-7777-7777-777777777777","name":"Cable Lat Pulldown",
            "choice":{"$type":"mostTrained","sessions":2},"progress":{"$type":"notTrained"},"sessionsLastFourWeeks":2,"series":[]}},
          {"family":"legs","lift":{"$type":"none"}}],
         "cardio":{"$type":"unknown","reason":"noHeartRate"}}
        """
    }

    private static func classified(_ pattern: String, _ primary: [String]) -> String {
        let muscles = primary.map { "\"\($0)\"" }.joined(separator: ",")
        let family = ["horizontalPush": "push", "verticalPull": "pull", "isolation": "push"][pattern] ?? "core"
        return #"{"$type":"classified","classification":{"pattern":"\#(pattern)","primary":[\#(muscles)],"secondary":[]},"source":{"$type":"map"},"family":"\#(family)"}"#
    }

    private static func movement(id: String, base: String, classification: String) -> String {
        let uuid = String(repeating: id, count: 8) + "-" + String(repeating: id, count: 4) + "-" + String(repeating: id, count: 4)
            + "-" + String(repeating: id, count: 4) + "-" + String(repeating: id, count: 12)
        return """
        {"movementDataID":"\(uuid)","equipmentID":"\(uuid)","equipment":{"equipmentID":"\(uuid)","name":"Machine"},
         "movementBaseID":"\(uuid)","movementBase":{"movementBaseID":"\(uuid)","name":"\(base)","description":"",
         "classification":\(classification)},"movementModifierID":null,"movementModifier":null,
         "setup":{"notes":"","addsToBodyweight":false}}
        """
    }

    private static let movementJSON = movement(id: "3", base: "Dip", classification: """
        {"$type":"classified","classification":{"pattern":"horizontalPush","primary":["chest","triceps"],"secondary":["frontDelts"]},
         "source":{"$type":"mapUnsure","reason":"between two presses"},"family":"push"}
        """)
}
