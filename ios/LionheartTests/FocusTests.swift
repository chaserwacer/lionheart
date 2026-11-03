import Foundation
import Testing

@testable import Lionheart

/// Focus pins as the server sends them, the terms the sheet can send, and how a pin reads against
/// the log.
@Suite("Focus")
struct FocusTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 20)
    private static let dip = "dddddddd-dddd-dddd-dddd-dddddddddddd"

    private static func day(_ month: Int, _ day: Int) -> CalendarDay {
        CalendarDay(year: 2026, month: month, day: day)
    }

    private static func pin(
        starts: CalendarDay = day(11, 2), ends: CalendarDay = day(12, 27),
        sessionsPerWeek: Int = 2, target: FocusTarget = .none, status: FocusStatus = .pinned
    ) -> FocusPin {
        FocusPin(
            focusPinId: UUID(), movementDataId: UUID(uuidString: dip)!,
            horizon: FocusHorizon(starts: starts, ends: ends),
            sessionsPerWeek: sessionsPerWeek, target: target, status: status)
    }

    private static func history(_ sessions: [(String, [(Double, Int)])]) throws -> MovementHistory {
        let json = sessions.enumerated().map { index, session in
            let sets = session.1.map { Session.liftSet(movementId: dip, reps: $0.1, weight: $0.0, rpe: "null") }
            return Session.completed(
                id: String(format: "00000000-0000-0000-0000-%012d", index + 1),
                date: session.0,
                sets: "[\(sets.joined(separator: ","))]",
                period: #"{"$type":"instant","at":"\#(session.0)T18:00:00+00:00"}"#)
        }
        let log = try Decoding.list(TrainingSession.self, "[\(json.joined(separator: ","))]")
        return MovementHistory.of(UUID(uuidString: dip)!, in: log, today: today)
    }

    // MARK: - The wire

    @Test("A pin decodes with its target and its unpinned day")
    func aPinDecodes() throws {
        let pin = try Decoding.decode(FocusPin.self, """
            {"focusPinId":"01a1a2bc-c8fa-7d97-8e4f-7a78eba2bb10","movementDataId":"\(Self.dip)",
             "starts":"2026-11-02","ends":"2027-01-31","sessionsPerWeek":2,
             "target":{"$type":"lift","weight":70,"unit":1,"reps":5},
             "status":{"$type":"unpinned","on":"2026-12-20"},
             "pinnedAt":"2026-11-03T18:47:23.642163"}
            """)

        #expect(pin.horizon == FocusHorizon(starts: Self.day(11, 2), ends: CalendarDay(year: 2027, month: 1, day: 31)))
        #expect(pin.target == .lift(weight: 70, unit: .pounds, reps: 5))
        #expect(pin.status == .unpinned(on: Self.day(12, 20)))
        #expect(!pin.isPinned)
    }

    @Test("An unknown target or status keeps the pin, as a case it cannot read")
    func unknownShapesKeepThePin() throws {
        let pin = try Decoding.decode(FocusPin.self, """
            {"focusPinId":"01a1a2bc-c8fa-7d97-8e4f-7a78eba2bb10","movementDataId":"\(Self.dip)",
             "starts":"2026-11-02","ends":"2027-01-31","sessionsPerWeek":2,
             "target":{"$type":"estimate","value":90},"status":{"$type":"paused"}}
            """)

        #expect(pin.target == .unrecognised("estimate"))
        #expect(pin.status == .unrecognised("paused"))
    }

    // MARK: - The draft

    @Test("Terms the server would refuse cannot be built", arguments: [
        (CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 11, day: 1), 2, FocusTarget.none, FocusDraft.Problem.endsBeforeItStarts),
        (CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 12, day: 1), 0, FocusTarget.none, .sessionsPerWeekOutOfRange),
        (CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 12, day: 1), 8, FocusTarget.none, .sessionsPerWeekOutOfRange),
        (CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 12, day: 1), 2, FocusTarget.lift(weight: 70, unit: .pounds, reps: 0), .targetRepsOutOfRange),
        (CalendarDay(year: 2026, month: 11, day: 2), CalendarDay(year: 2026, month: 12, day: 1), 2, FocusTarget.lift(weight: -5, unit: .pounds, reps: 5), .targetWeightNegative),
    ])
    func refusedTerms(
        starts: CalendarDay, ends: CalendarDay, sessionsPerWeek: Int, target: FocusTarget,
        problem: FocusDraft.Problem
    ) {
        #expect(throws: problem) {
            try FocusDraft.make(starts: starts, ends: ends, sessionsPerWeek: sessionsPerWeek, target: target)
        }
    }

    @Test("A one-day horizon with no target is valid")
    func aOneDayHorizon() throws {
        let draft = try FocusDraft.make(
            starts: Self.day(11, 2), ends: Self.day(11, 2), sessionsPerWeek: 1, target: .none)

        #expect(draft.horizon.dayCount() == 1)
    }

    @Test("Eight weeks from a Monday ends on the Sunday seven weeks later")
    func lengthsEndTheDayBefore() {
        #expect(FocusLength.eightWeeks.ends(startingOn: Self.day(11, 2)) == Self.day(12, 27))
        #expect(FocusLength.year.ends(startingOn: Self.day(11, 2)) == CalendarDay(year: 2027, month: 11, day: 1))
    }

    // MARK: - Reading a pin

    @Test("A pin reads as upcoming, running, past its horizon, or unpinned")
    func phases() {
        #expect(FocusReading.phase(of: Self.pin(starts: Self.day(11, 23)), today: Self.today) == .upcoming(startsInDays: 3))
        #expect(FocusReading.phase(of: Self.pin(), today: Self.today) == .running(day: 19, of: 56))
        #expect(FocusReading.phase(of: Self.pin(ends: Self.day(11, 19)), today: Self.today) == .horizonOver)
        #expect(FocusReading.phase(of: Self.pin(status: .unpinned(on: Self.day(11, 10))), today: Self.today)
            == .unpinned(on: Self.day(11, 10)))
    }

    @Test("Attendance counts the sessions inside the horizon so far, against the frequency asked")
    func attendance() throws {
        let history = try Self.history([
            ("2026-10-28", [(40, 5)]),
            ("2026-11-04", [(40, 6)]),
            ("2026-11-12", [(45, 5)]),
        ])

        let reading = FocusReading.of(Self.pin(), history: history, today: Self.today)

        #expect(reading.attendance.sessions == 2)
        #expect(reading.attendance.elapsedDays == 19)
        #expect(abs(reading.attendance.expectedSoFar - 2 * 19.0 / 7) < 0.0001)
        #expect(reading.attendance.expectedOverHorizon == 16)
        #expect(reading.lastDone == Self.day(11, 12))
    }

    @Test("An unpinned pin's reading stops on the day it was unpinned")
    func unpinningClosesTheWindow() throws {
        let history = try Self.history([("2026-11-04", [(40, 6)]), ("2026-11-12", [(45, 5)])])

        let reading = FocusReading.of(
            Self.pin(status: .unpinned(on: Self.day(11, 8))), history: history, today: Self.today)

        #expect(reading.attendance.sessions == 1)
        #expect(reading.attendance.elapsedDays == 7)
    }

    @Test("Progress is the estimated single before the horizon against the best inside it")
    func progress() throws {
        let history = try Self.history([
            ("2026-10-20", [(30, 10)]),
            ("2026-10-28", [(40, 5)]),
            ("2026-11-04", [(45, 6)]),
            ("2026-11-12", [(45, 5)]),
        ])

        let progress = try #require(FocusReading.of(Self.pin(), history: history, today: Self.today).progress)

        #expect(progress.before?.day == Self.day(10, 28))
        #expect(progress.best?.day == Self.day(11, 4))
        #expect(progress.latest?.day == Self.day(11, 12))
        #expect(abs((progress.change ?? 0) - (54 - 40 * (1 + 5.0 / 30))) < 0.0001)
    }

    @Test("A target is met by the first set at its load for its reps or more inside the horizon")
    func aTargetIsMet() throws {
        let history = try Self.history([
            ("2026-10-28", [(70, 6)]),
            ("2026-11-04", [(65, 6)]),
            ("2026-11-12", [(70, 7)]),
            ("2026-11-18", [(75, 5)]),
        ])

        let reading = FocusReading.of(
            Self.pin(target: .lift(weight: 70, unit: .pounds, reps: 5)), history: history, today: Self.today)

        // The 28 October set is before the pin, so it does not meet it.
        #expect(reading.target == .met(on: Self.day(11, 12), weight: 70, reps: 7))
    }

    @Test("A target not yet met says how far the heaviest set at its reps is from it")
    func aTargetToGo() throws {
        let history = try Self.history([("2026-11-04", [(50, 7), (60, 3)]), ("2026-11-12", [(55, 5)])])

        let reading = FocusReading.of(
            Self.pin(target: .lift(weight: 70, unit: .pounds, reps: 5)), history: history, today: Self.today)

        #expect(reading.target == .toGo(weight: 70, unit: .pounds, reps: 5, bestAtReps: 55))
        #expect(reading.target.remaining == 15)
    }

    @Test("A target whose reps no set has reached gives no distance rather than a false one")
    func aTargetWithNoSetAtItsReps() throws {
        let history = try Self.history([("2026-11-04", [(60, 3)])])

        let reading = FocusReading.of(
            Self.pin(target: .lift(weight: 70, unit: .pounds, reps: 5)), history: history, today: Self.today)

        #expect(reading.target == .toGo(weight: 70, unit: .pounds, reps: 5, bestAtReps: nil))
        #expect(reading.target.remaining == nil)
    }

    @Test("Current pins lead, soonest to end first; past ones follow, latest ended first")
    func arrangement() throws {
        let history = try Self.history([])
        let readings = [
            Self.pin(ends: Self.day(12, 27)),
            Self.pin(ends: Self.day(11, 30)),
            Self.pin(ends: Self.day(11, 10)),
            Self.pin(status: .unpinned(on: Self.day(11, 15))),
        ].map { FocusReading.of($0, history: history, today: Self.today) }

        let arranged = readings.arrangedForReading()

        #expect(arranged.current.map(\.pin.horizon.ends) == [Self.day(11, 30), Self.day(12, 27)])
        #expect(arranged.past.map(\.pin.id) == [readings[3].pin.id, readings[2].pin.id])
    }
}
