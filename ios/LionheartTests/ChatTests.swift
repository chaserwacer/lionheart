import Foundation
import Testing

@testable import Lionheart

/// Who said what, which the client has to be told rather than infer.
///
/// These are the tests that pin the reason the server grew a `role` member: the chronological
/// projection is a merge of two tables, so guessing the speaker from a turn's position is wrong the
/// moment one turn does not arrive in a user/model pair.
@Suite("Chat roles")
struct ChatRoleTests {
    @Test("The role is read from the wire, not from the turn's position")
    func theRoleIsReceived() throws {
        let conversation = try Decoding.decode(ChatConversation.self, Chat.conversation(
            messages: """
                [\(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 1, content: "An answer",
                    createdAt: "2026-03-04T17:31:00")),
                 \(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 0, content: "A question",
                    createdAt: "2026-03-04T17:32:00"))]
                """))

        // The answer is first in the array. Index parity would call it the athlete's.
        #expect(conversation.messages[0].role == .model)
        #expect(conversation.messages[1].role == .athlete)
    }

    @Test("Only the athlete's and the model's turns reach the transcript")
    func toolAndSystemTurnsAreNotConversation() {
        #expect(ChatRole.athlete.isShownInTranscript)
        #expect(ChatRole.model.isShownInTranscript)
        // A tool result rendered as the model's answer is the failure this answer prevents.
        #expect(!ChatRole.tool.isShownInTranscript)
        #expect(!ChatRole.system.isShownInTranscript)
        #expect(!ChatRole.unrecognised(9).isShownInTranscript)
    }

    @Test("A role this build has not heard of is kept out of the transcript rather than guessed")
    func anUnknownRoleIsNotSpoken() throws {
        let conversation = try Decoding.decode(ChatConversation.self, Chat.conversation(
            messages: """
                [\(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "A question")),
                 \(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03", role: 7, content: "Something new",
                    createdAt: "2026-03-04T17:33:00"))]
                """))

        #expect(conversation.messages.count == 2)
        #expect(conversation.transcript.map(\.text) == ["A question"])
    }

    @Test("A stored tool result is dropped from the transcript it does not belong in")
    func toolResultsAreNotShown() throws {
        let conversation = try Decoding.decode(ChatConversation.self, Chat.conversation(
            messages: """
                [\(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "A question")),
                 \(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee04", role: 2,
                    content: "GetWellnessStates returned 5 rows", createdAt: "2026-03-04T17:33:00")),
                 \(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "An answer",
                    createdAt: "2026-03-04T17:34:00"))]
                """))

        #expect(conversation.transcript.map(\.text) == ["A question", "An answer"])
    }

    @Test("The transcript runs oldest first, whatever order the projection sent")
    func theTranscriptIsChronological() throws {
        let conversation = try Decoding.decode(ChatConversation.self, Chat.conversation(
            messages: """
                [\(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "second",
                    createdAt: "2026-03-04T17:34:00")),
                 \(Chat.message(
                    id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "first",
                    createdAt: "2026-03-04T17:30:00"))]
                """))

        #expect(conversation.transcript.map(\.text) == ["first", "second"])
    }

    @Test("A conversation the list route sent has no turns, and that is not a failure")
    func nullMessagesDecodeAsNone() throws {
        // The list route sends `"messages": null`. A client that threw on it could not show the
        // conversation picker at all.
        let conversation = try Decoding.decode(
            ChatConversation.self, Chat.conversationWithNullMessages())

        #expect(conversation.messages.isEmpty)
        #expect(conversation.name == "Listed")
    }

    @Test("A conversation with no name is still nameable on screen")
    func anUnnamedConversationGetsATitle() throws {
        let conversation = try Decoding.decode(
            ChatConversation.self, Chat.conversation(name: ""))

        #expect(conversation.title == "Untitled conversation")
    }
}

/// The Chat screen's state, whose job is to never lose the athlete's question.
@Suite("Chat conversation state")
struct ChatConversationStateTests {
    private static func conversation(messages: String = "[]") throws -> ChatConversation {
        try Decoding.decode(ChatConversation.self, Chat.conversation(messages: messages))
    }

    @Test("A failed read never discards a conversation already on screen")
    func aFailedReadKeepsTheTranscript() throws {
        // Losing a transcript to a timed-out refresh would lose the one thing the athlete came
        // back to read.
        let reading = ChatConversationState.reading(try Self.conversation(
            messages: "[\(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "A question"))]"))

        let after = reading.applying(.failure(.offline("dropped")))

        #expect(after == reading)
        #expect(after.turns.count == 1)
    }

    @Test("A failed first read has nothing to keep, so it is unavailable")
    func aFailedFirstReadIsUnavailable() {
        #expect(ChatConversationState.loading.applying(.failure(.notFound))
            == .unavailable(.notFound))
    }

    @Test("A pending question is on screen the moment it is sent")
    func thePendingQuestionIsShown() throws {
        // An exchange takes tens of seconds. A screen that swallowed the question for that long
        // reads as broken.
        let pending = ChatTurn.pending(id: UUID(), text: "Should I press overhead?")
        let state = ChatConversationState.awaitingReply(try Self.conversation(), pending: pending)

        #expect(state.turns == [pending])
        #expect(state.isAwaitingReply)
        #expect(!state.acceptsTyping)
    }

    @Test("A refused exchange hands the question back, because the server did not keep it")
    func aRefusedQuestionIsHandedBack() throws {
        // The server stages the athlete's message and saves it only once the model has answered,
        // so a refused exchange leaves no record of the question at all.
        let unsent = ChatTurn.unsent(
            id: UUID(), text: "Should I press overhead?", failure: .offline("dropped"))
        let state = ChatConversationState.refused(try Self.conversation(), unsent: unsent)

        #expect(state.unsentText == "Should I press overhead?")
        #expect(state.acceptsTyping)
        #expect(!state.isAwaitingReply)
    }

    @Test("Nothing is handed back when nothing failed")
    func noUnsentTextInTheOrdinaryStates() throws {
        #expect(ChatConversationState.loading.unsentText == nil)
        #expect(ChatConversationState.none.unsentText == nil)
        #expect(ChatConversationState.reading(try Self.conversation()).unsentText == nil)
        #expect(ChatConversationState.unavailable(.notFound).unsentText == nil)
    }

    @Test("The composer is open in every state except a reply in flight")
    func onlyAnOutstandingReplyLocksTheComposer() throws {
        #expect(ChatConversationState.none.acceptsTyping)
        #expect(ChatConversationState.reading(try Self.conversation()).acceptsTyping)
        #expect(!ChatConversationState.loading.acceptsTyping)
        #expect(!ChatConversationState.unavailable(.notFound).acceptsTyping)
    }

    @Test("No conversation is an empty state, with no conversation behind it")
    func noneCarriesNothing() {
        #expect(ChatConversationState.none.conversation == nil)
        #expect(ChatConversationState.none.turns.isEmpty)
    }
}

/// Which side of the conversation a turn sits on.
@Suite("Chat turns")
struct ChatTurnTests {
    @Test("A pending and an unsent turn are both the athlete's")
    func theAthletesOwnTurnsAreTheirs() {
        let id = UUID()

        #expect(ChatTurn.pending(id: id, text: "x").isFromAthlete)
        #expect(ChatTurn.unsent(id: id, text: "x", failure: .notFound).isFromAthlete)
    }

    @Test("A model turn is not the athlete's, whatever its position")
    func aModelTurnIsNotTheAthletes() throws {
        let message = try Decoding.decode(
            ChatMessage.self,
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "An answer"))

        #expect(!ChatTurn.said(message).isFromAthlete)
    }

    @Test("A turn keeps its own identity across the states it moves through")
    func aTurnsIdentitySurvivesFailing() {
        let id = UUID()
        let pending = ChatTurn.pending(id: id, text: "x")
        let unsent = ChatTurn.unsent(id: id, text: "x", failure: .notFound)

        // The same bubble, not a second one appearing beside it.
        #expect(pending.id == unsent.id)
    }
}

/// Naming a conversation after its first question, since the server does no auto-naming and an
/// unnamed chat is unfindable a month later.
@Suite("Conversation titles")
struct ConversationTitleTests {
    @Test("A short question is the title unchanged")
    func shortQuestionsAreKeptWhole() {
        #expect(ChatStore.title(from: "Am I recovered?") == "Am I recovered?")
    }

    @Test("A long question is clipped on a word boundary, not mid-word")
    func longQuestionsAreClippedOnAWord() {
        let title = ChatStore.title(
            from: "Given my last few sessions and my shoulder, should I press overhead this week?")

        #expect(title.hasSuffix("…"))
        #expect(!title.contains("shoulde…"))
        #expect(title.count <= 50)
    }

    @Test("Newlines and runs of spaces collapse, so a title stays one line")
    func whitespaceIsCollapsed() {
        #expect(ChatStore.title(from: "How   am\nI\tdoing?") == "How am I doing?")
    }
}

/// The context card, which is the premise behind every answer on the screen.
@Suite("Athlete context card")
struct AthleteContextCardTests {
    @Test("Both tiers arrive with the instant each was computed at")
    func bothTiersCarryTheirFreshness() throws {
        // Advice drawn from a Recent State computed six hours ago reads differently once you can
        // see when it was computed, which is why the as-of instants travel with the prose.
        let card = try Decoding.decode(AthleteContextCard.self, ContextCard.card())

        #expect(card.stableProfile == "Trains four times a week around a shoulder.")
        #expect(card.stableProfileAsOf != nil)
        #expect(card.recentStateAsOf != nil)
        #expect(!card.isEmpty)
    }

    @Test("A card with no prose in it is empty, so the screen says so rather than showing blanks")
    func aCardWithoutProseIsEmpty() throws {
        let card = try Decoding.decode(AthleteContextCard.self, ContextCard.card(stable: "", recent: ""))

        #expect(card.isEmpty)
    }
}
