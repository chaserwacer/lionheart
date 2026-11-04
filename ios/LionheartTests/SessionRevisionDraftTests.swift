import Foundation
import Testing

@testable import Lionheart

/// Correcting a finished session on the device: what can be opened, how the sets are changed, and
/// what saving will mean for the records.
@Suite("Session revision draft")
struct SessionRevisionDraftTests {
    private static let bench = UUID(uuidString: "22222222-2222-2222-2222-222222222222")!
    private static let squat = UUID(uuidString: "cccccccc-cccc-cccc-cccc-cccccccccccc")!
    private static let start = Date(timeIntervalSince1970: 1_780_000_000)

    @Test("A completed session opens with its sets, its time and its notes")
    func aCompletedSessionOpens() throws {
        let draft = try Self.draft(sets: [Self.lift(Self.bench, 225), Self.lift(Self.bench, 235)])

        #expect(draft.commands.count == 2)
        #expect(draft.start == Self.start)
        #expect(draft.end == Self.start.addingTimeInterval(3600))
        #expect(draft.notes == "as logged")
        #expect(!draft.hasChanges)
    }

    @Test("Only a completed session can be corrected")
    func onlyACompletedSessionOpens() throws {
        let planned = try Decoding.decode(TrainingSession.self, Session.planned())

        #expect(SessionRevisionDraft.of(planned) == .failure(.notCompleted))
    }

    @Test("A session holding a set this build cannot state refuses, rather than losing the set on save")
    func anUnreadableSetRefuses() throws {
        let session = Self.completed(sets: [
            Self.lift(Self.bench, 225),
            .unrecognised(type: "sled", common: .init(source: .formed, facets: [])),
        ])

        #expect(SessionRevisionDraft.of(session) == .failure(.unreadableSet))
    }

    @Test("Replacing and removing a set is by its own id, so the rest keep their place")
    func setsAreAddressedById() throws {
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225), Self.lift(Self.bench, 235), Self.lift(Self.bench, 245)])
        let middle = draft.sets[1].id

        draft.remove(draft.sets[0].id)
        draft.replace(middle, with: Self.command(Self.bench, 240))

        #expect(draft.commands == [Self.command(Self.bench, 240), Self.command(Self.bench, 245)])
        #expect(draft.hasChanges)
        #expect(draft.reachesRecords)
    }

    @Test("A new set goes at the end of its own block, not the end of the session")
    func aNewSetJoinsItsBlock() throws {
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225), Self.lift(Self.squat, 315)])
        let benchBlock = try #require(draft.blocks.first)

        draft.append(Self.command(Self.bench, 230), to: benchBlock)

        #expect(draft.commands == [Self.command(Self.bench, 225), Self.command(Self.bench, 230), Self.command(Self.squat, 315)])
    }

    @Test("A movement picked waits as an empty block, and joins the sets with its first one")
    func anAddedMovementWaitsForItsFirstSet() throws {
        let deadlift = UUID()
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225)])

        draft.addMovement(deadlift)
        let waiting = try #require(draft.blocks.last)
        #expect(waiting.movementDataId == deadlift)
        #expect(waiting.sets.isEmpty)
        #expect(!draft.hasChanges)

        draft.append(Self.command(deadlift, 405), to: waiting)

        #expect(draft.blocks.map(\.movementDataId) == [Self.bench, deadlift])
        #expect(draft.blocks.allSatisfy { !$0.sets.isEmpty })
    }

    @Test("A new set on a movement with none starts empty, in the unit the session lifts in")
    func aFirstSetStartsEmptyInTheSessionsUnit() throws {
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225)])
        draft.addMovement(Self.squat)

        let fields = draft.newSetDraft(for: try #require(draft.blocks.last))

        #expect(fields.weight.isEmpty)
        #expect(fields.weightUnit == .pounds)
        #expect(fields.movementDataId == Self.squat)
    }

    @Test("Moving the start keeps the session's length, and reaches the records")
    func movingTheStartKeepsTheLength() throws {
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225)])
        let dayEarlier = Self.start.addingTimeInterval(-86_400)

        draft.moveStart(to: dayEarlier)

        #expect(draft.end == dayEarlier.addingTimeInterval(3600))
        #expect(draft.reachesRecords)
    }

    @Test("Notes and the finish alone do not reach the records")
    func notesAndTheFinishDoNotReachTheRecords() throws {
        var draft = try Self.draft(sets: [Self.lift(Self.bench, 225)])

        draft.notes = "felt heavy"
        draft.setEnd(Self.start.addingTimeInterval(5400))

        #expect(draft.hasChanges)
        #expect(!draft.reachesRecords)
    }

    @Test("A session with no sets, or one finishing before it starts, is not saved")
    func anImpossibleSessionIsNotSavable() throws {
        var emptied = try Self.draft(sets: [Self.lift(Self.bench, 225)])
        emptied.remove(emptied.sets[0].id)
        #expect(emptied.problem != nil)
        #expect(!emptied.isSavable)

        var backwards = try Self.draft(sets: [Self.lift(Self.bench, 225)])
        backwards.setEnd(Self.start.addingTimeInterval(-60))
        #expect(backwards.problem != nil)
    }

    // MARK: - Fixtures

    private static func draft(sets: [SetEntry]) throws -> SessionRevisionDraft {
        try SessionRevisionDraft.of(completed(sets: sets)).get()
    }

    private static func completed(sets: [SetEntry]) -> TrainingSession {
        .completed(TrainingSession.Completed(
            envelope: .init(
                trainingSessionId: UUID(), notes: "as logged", provenance: .user, trainingProgramId: nil,
                createdAt: nil, date: CalendarDay(start)),
            sets: sets,
            period: .interval(start: start, end: start.addingTimeInterval(3600)),
            effort: .notReported,
            decoration: .none))
    }

    private static func lift(_ movement: UUID, _ weight: Double) -> SetEntry {
        .lift(.init(
            common: .init(source: .formed, facets: []), movementDataId: movement, reps: 5, weight: weight,
            unit: .pounds, rpe: .unrated))
    }

    private static func command(_ movement: UUID, _ weight: Double) -> SetCommand {
        .lift(.init(movementDataId: movement, reps: 5, weight: weight, unit: .pounds, rpe: .unrated))
    }
}

@Suite("Durations on the wire")
struct WireDurationTests {
    @Test("A fractional duration is sent as it was stored, so an untouched set is not a changed one")
    func fractionsSurvive() {
        #expect(WireDuration.wire(fromSeconds: 1470.25) == "00:24:30.250")
        #expect(WireDuration.wire(fromSeconds: 1470) == "00:24:30")
        #expect(WireDuration.seconds(from: WireDuration.wire(fromSeconds: 1470.25)) == 1470.25)
    }
}
