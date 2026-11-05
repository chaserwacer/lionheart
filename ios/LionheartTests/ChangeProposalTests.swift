import Foundation
import Testing

@testable import Lionheart

/// A change Tempo proposed, as the thread reads it: decoded totally, placed after the answer that
/// proposed it, replaced in its session once answered, and named while it is being drafted.
@Suite("Change proposals")
struct ChangeProposalTests {
    private static let sessionId = "aaaaaaaa-0000-0000-0000-000000000001"
    private static let proposalId = "cccccccc-0000-0000-0000-000000000001"

    private static func proposal(
        id: String = Self.proposalId,
        proposedAt: String = "2026-11-04T17:00:05Z",
        area: String = "plan",
        answer: String = #"{"$type":"waiting"}"#
    ) -> String {
        """
        {"changeProposalId":"\(id)","proposedAt":"\(proposedAt)","area":"\(area)",
         "title":"Plan a session on Wed 11 Nov","lines":["Easy run, 4 miles","Note: with friends"],
         "answer":\(answer)}
        """
    }

    private static func thread(proposals: [String], messages: String = "[]") throws -> ChatThread {
        try Decoding.decode(ChatThread.self, """
            {"sessions":[{"sessionId":"\(sessionId)","openedAt":"2026-11-04T16:00:00Z","messages":\(messages),
              "proposals":[\(proposals.joined(separator: ","))]}],"hasEarlier":false}
            """)
    }

    @Test("A waiting card decodes with what it will change")
    func aWaitingCardDecodes() throws {
        let proposal = try Decoding.decode(ChangeProposal.self, Self.proposal())

        #expect(proposal.title == "Plan a session on Wed 11 Nov")
        #expect(proposal.lines == ["Easy run, 4 miles", "Note: with friends"])
        #expect(proposal.area == .plan)
        #expect(proposal.answer == .waiting)
        #expect(proposal.answer.isWaiting)
    }

    @Test("A refused confirmation keeps the service's reason")
    func aRefusalKeepsItsReason() throws {
        let proposal = try Decoding.decode(ChangeProposal.self, Self.proposal(answer: """
            {"$type":"refused","at":"2026-11-04T17:02:00","reason":"That day is already marked non-training."}
            """))

        guard case .refused(_, let reason) = proposal.answer else {
            Issue.record("Answer was \(proposal.answer)")
            return
        }
        #expect(reason == "That day is already marked non-training.")
        #expect(!proposal.answer.isWaiting)
    }

    @Test("An answer or an area from a newer server is unrecognised, and the card still renders")
    func unknownCasesAreUnrecognised() throws {
        let proposal = try Decoding.decode(ChangeProposal.self, Self.proposal(
            area: "nutrition", answer: #"{"$type":"withdrawn"}"#))

        #expect(proposal.area == .unrecognised("nutrition"))
        #expect(proposal.answer == .unrecognised("withdrawn"))
    }

    @Test("A card with no id is dropped, not the session it was in")
    func aCardWithoutAnIdIsDropped() throws {
        let thread = try Self.thread(proposals: [
            #"{"title":"No id","answer":{"$type":"waiting"}}"#,
            Self.proposal(),
        ])

        #expect(thread.sessions.first?.proposals.count == 1)
    }

    @Test("A card follows the answer that proposed it and comes before anything said after")
    func aCardFollowsItsAnswer() throws {
        let thread = try Self.thread(proposals: [Self.proposal(proposedAt: "2026-11-04T17:00:05Z")], messages: """
            [\(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "Running Wednesday", createdAt: "2026-11-04T16:59:00")),
             \(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "On a card for you", createdAt: "2026-11-04T17:00:04")),
             \(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03", role: 0, content: "Thanks", createdAt: "2026-11-04T17:05:00"))]
            """)

        let rows = ThreadTranscript.rows(thread, tail: nil)

        #expect(rows.map(\.id) == [
            "opened-\(Self.sessionId.uppercased())",
            "EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEE01",
            "EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEE02",
            "proposal-\(Self.proposalId.uppercased())",
            "EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEE03",
        ])
    }

    @Test("An answered card replaces the waiting one in its session")
    func anAnsweredCardReplacesTheWaitingOne() throws {
        let thread = try Self.thread(proposals: [Self.proposal()])
        let confirmed = try Decoding.decode(ChangeProposal.self, Self.proposal(answer: """
            {"$type":"confirmed","at":"2026-11-04T17:01:00"}
            """))

        let answered = thread.with(confirmed)

        #expect(answered.sessions.first?.proposals.first?.answer == confirmed.answer)
        #expect(answered.sessions.first?.proposals.count == 1)
    }

    @Test("A round of tools that proposes reads as drafting, not reading")
    func proposingToolsReadAsDrafting() {
        #expect(ReplyProgress.isProposing(["GetPlannedDays", "ProposePlanSession"]))
        #expect(!ReplyProgress.isProposing(["GetPlannedDays", "FindMovements"]))
    }

    @Test("A card answered elsewhere or gone means the thread is read again; a lost connection does not")
    func outdatedCardsAreReadAgain() {
        #expect(ProposalAnswering.refused(.refused("That change has already been answered.")).revealsOutdatedCard)
        #expect(ProposalAnswering.refused(.notFound).revealsOutdatedCard)
        #expect(!ProposalAnswering.refused(.offline("No connection.")).revealsOutdatedCard)
        #expect(!ProposalAnswering.answering(UUID()).revealsOutdatedCard)
    }
}
