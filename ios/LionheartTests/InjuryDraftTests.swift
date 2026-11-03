import Foundation
import Testing

@testable import Lionheart

/// What the injury forms will send.
@Suite("Injury drafts")
struct InjuryDraftTests {
    private static let today = CalendarDay(year: 2026, month: 11, day: 3)

    @Test("An injury needs a name and a start that is not in the future")
    func draftRefusals() {
        #expect(throws: InjuryDraft.Problem.unnamed) {
            try InjuryDraft.make(name: "   ", startedOn: Self.today, notes: "", today: Self.today)
        }
        #expect(throws: InjuryDraft.Problem.startsInTheFuture) {
            try InjuryDraft.make(
                name: "Hamstring", startedOn: Self.today.adding(days: 1), notes: "", today: Self.today)
        }
    }

    @Test("An accepted draft is trimmed")
    func draftTrims() throws {
        let draft = try InjuryDraft.make(
            name: "  Right hamstring ", startedOn: Self.today, notes: "  \n", today: Self.today)

        #expect(draft.name == "Right hamstring")
        #expect(draft.notes.isEmpty)
    }

    @Test("A pain level is held to the server's scale")
    func painIsClamped() {
        #expect(InjuryEventDraft(kind: .checkIn, painLevel: 14, notes: "").painLevel == 10)
        #expect(InjuryEventDraft(kind: .checkIn, painLevel: -1, notes: "").painLevel == 0)
    }

    @Test("Event kinds cross the wire as the server's integers")
    func kindsMatchTheServer() {
        // InjuryEventType is `treatment, checkin`, and `InjuryEvent.Kind` decodes the same order.
        #expect(InjuryEventDraft.Kind.treatment.wire == 0)
        #expect(InjuryEventDraft.Kind.checkIn.wire == 1)
        #expect(InjuryEvent.Kind(wire: InjuryEventDraft.Kind.checkIn.wire) == .checkIn)
    }
}
