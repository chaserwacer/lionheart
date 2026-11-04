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
        #expect(volume.family(.pull) == FamilyVolume(family: .pull, hardSets: 3, target: 8, lastTrained: CalendarDay(year: 2026, month: 10, day: 28)))
        #expect(volume.patterns.first { $0.pattern == .isolation }?.target == nil)
        #expect(volume.muscles.first { $0.muscle == .upperBack }?.sets == 1.5)
        #expect(volume.unplaced.map(\.name) == ["Cable Mystery"])
        #expect(volume.upNext == .train(
            families: [.push, .pull], lastTrained: CalendarDay(year: 2026, month: 10, day: 28),
            trainedToday: false, readiness: .low(ouraScore: 58, usualLow: 63, usualHigh: 87)))
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
        #expect(score.lifting.families.map(\.hardSets) == [4, 3, 7])
        #expect(score.cardio.measure == .measured(
            zone2Minutes: 67.1, zone2Target: 150, highIntensityMinutes: 0, highIntensityTarget: 20,
            sessions: 5, heartRateDays: 7))
        #expect(score.consistency.sessionsPerBlock == [2, 2, 1, 0])

        let partial = try Decoding.decode(
            TrainingScore.self,
            Self.scoreJSON(cardio: #"{"points":null,"outOf":25,"measure":{"$type":"unknown","reason":"noHeartRate"}}"#,
                           unknown: #"["cardio"]"#))
        #expect(partial.cardio.points == nil)
        #expect(partial.cardio.measure == .unknown(.noHeartRate))
        #expect(partial.unknown == [.cardio])
        #expect((score.outOf, partial.outOf) == (100, 75))
        #expect(ScoreBreakdown.cardio(partial.cardio.measure).isEmpty)
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
        let measures = ScoreBreakdown.consistency(ConsistencyPart(points: 0, outOf: 20, sessionsPerBlock: [2, 3, 0, 1], target: 3))

        #expect(measures.map(\.label) == ["Last 7 days", "7–13 days ago", "14–20 days ago", "21–27 days ago"])
        #expect(measures.map(\.share) == [2.0 / 3, 1, 0, 1.0 / 3])
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
    {"points":6.7,"outOf":25,"measure":{"$type":"measured","zone2Minutes":67.1,"zone2Target":150,
     "highIntensityMinutes":0,"highIntensityTarget":20,"sessions":5,"heartRateDays":7}}
    """

    private static func scoreJSON(cardio: String, unknown: String = "[]") -> String {
        """
        {"window":{"from":"2026-10-28","to":"2026-11-03"},"value":49,"outOf":\(unknown == "[]" ? 100 : 75),"unknown":\(unknown),
         "lifting":{"points":20.4,"outOf":35,"families":[
            {"family":"push","hardSets":4,"target":8,"lastTrained":"2026-10-28"},
            {"family":"pull","hardSets":3,"target":8,"lastTrained":"2026-10-28"},
            {"family":"legs","hardSets":7,"target":8,"lastTrained":"2026-11-02"}]},
         "cardio":\(cardio),
         "progress":{"points":13.3,"outOf":20,"families":[]},
         "consistency":{"points":8.3,"outOf":20,"sessionsPerBlock":[2,2,1,0],"target":3}}
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
