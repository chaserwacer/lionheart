import Foundation
import Testing

@testable import Lionheart

/// What the Train tab offers, read from one window of sessions.
@Suite("Training plan")
struct TrainingPlanTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 2)

    private static func envelope(
        _ id: UUID = UUID(), on day: CalendarDay, notes: String = ""
    ) -> TrainingSession.Envelope {
        TrainingSession.Envelope(
            trainingSessionId: id,
            notes: notes,
            provenance: .user,
            trainingProgramId: nil,
            createdAt: nil,
            date: day)
    }

    private static func planned(for day: CalendarDay, notes: String = "") -> TrainingSession {
        .planned(TrainingSession.Planned(
            envelope: envelope(on: day, notes: notes), plannedFor: day, prescriptions: []))
    }

    private static func active(startedAt: Date) -> TrainingSession {
        .active(TrainingSession.Active(
            envelope: envelope(on: today), startedAt: startedAt, sets: []))
    }

    private static func completed(on day: CalendarDay) -> TrainingSession {
        .completed(TrainingSession.Completed(
            envelope: envelope(on: day), sets: [], period: .instant(at: Date()),
            effort: .notReported, decoration: .none))
    }

    @Test("A session planned for today is today's")
    func todayIsToday() {
        let plan = TrainingPlan.reading(
            sessions: [Self.planned(for: Self.today)], today: Self.today)

        #expect(plan.today.count == 1)
        #expect(plan.upcoming.isEmpty)
        #expect(plan.missed.isEmpty)
    }

    @Test("A session planned for a later day is coming up, soonest first")
    func upcomingIsSoonestFirst() {
        let plan = TrainingPlan.reading(
            sessions: [
                Self.planned(for: CalendarDay(year: 2026, month: 11, day: 9), notes: "later"),
                Self.planned(for: CalendarDay(year: 2026, month: 11, day: 4), notes: "sooner"),
            ],
            today: Self.today)

        #expect(plan.upcoming.map(\.envelope.notes) == ["sooner", "later"])
    }

    @Test("A session planned for a day gone by has been missed, not scheduled")
    func pastPlansAreMissed() {
        // The same rule the Today screen applies, so the two screens cannot disagree about one
        // session.
        let plan = TrainingPlan.reading(
            sessions: [Self.planned(for: CalendarDay(year: 2026, month: 10, day: 30))],
            today: Self.today)

        #expect(plan.missed.count == 1)
        #expect(plan.today.isEmpty)
        #expect(plan.upcoming.isEmpty)
    }

    @Test("Missed sessions are most recent first, because that is the one to start late")
    func missedIsMostRecentFirst() {
        let plan = TrainingPlan.reading(
            sessions: [
                Self.planned(for: CalendarDay(year: 2026, month: 10, day: 20), notes: "older"),
                Self.planned(for: CalendarDay(year: 2026, month: 10, day: 30), notes: "newer"),
            ],
            today: Self.today)

        #expect(plan.missed.map(\.envelope.notes) == ["newer", "older"])
    }

    @Test("The session in progress is the one most recently started")
    func theLatestActiveSessionWins() {
        // The server will hold several active sessions at once; the device owns one workout, so
        // the plan names the newest rather than whichever came back first.
        let plan = TrainingPlan.reading(
            sessions: [
                Self.active(startedAt: Date(timeIntervalSince1970: 1000)),
                Self.active(startedAt: Date(timeIntervalSince1970: 5000)),
            ],
            today: Self.today)

        #expect(plan.inProgress?.startedAt == Date(timeIntervalSince1970: 5000))
    }

    @Test("Completed and skipped sessions are History's, not this tab's")
    func finishedSessionsAreIgnored() {
        let plan = TrainingPlan.reading(
            sessions: [
                Self.completed(on: Self.today),
                .skipped(TrainingSession.Skipped(
                    envelope: Self.envelope(on: Self.today), plannedFor: Self.today)),
            ],
            today: Self.today)

        #expect(plan.hasNothingPlanned)
        #expect(plan.inProgress == nil)
    }

    @Test("A lifecycle state this build has not heard of is left alone rather than guessed at")
    func anUnknownStateIsIgnored() {
        let plan = TrainingPlan.reading(
            sessions: [.unrecognised(type: "abandoned", envelope: Self.envelope(on: Self.today))],
            today: Self.today)

        #expect(plan.hasNothingPlanned)
        #expect(plan.inProgress == nil)
    }

    @Test("An empty window is nothing planned")
    func anEmptyWindowIsEmpty() {
        #expect(TrainingPlan.reading(sessions: [], today: Self.today).hasNothingPlanned)
    }
}
