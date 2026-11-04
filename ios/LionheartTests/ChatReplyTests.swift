import Foundation
import Testing

@testable import Lionheart

/// The thread's answer arriving as it is written: the events as the server sends them, what the
/// screen makes of each, and finding the answer in the thread after the connection went.
@Suite("Chat reply")
struct ChatReplyTests {
    // MARK: - Reading the events

    @Test("An event is named by its event line and dispatched by its data line; comments pass by")
    func readingEvents() {
        var reader = ServerSentEventReader()
        let lines = [
            ": still working",
            "event: delta",
            #"data: {"text":"Rest "}"#,
            "event: done",
            "data:{}",
            #"data: {"orphan":true}"#,
        ]

        let events = lines.compactMap { reader.read($0) }

        #expect(events == [
            ServerSentEvent(name: "delta", data: #"{"text":"Rest "}"#),
            ServerSentEvent(name: "done", data: "{}"),
            ServerSentEvent(name: "message", data: #"{"orphan":true}"#),
        ])
    }

    @Test("Each event the server sends reads as what it means")
    func decodingReplies() throws {
        let session = """
            {"sessionId":"aaaaaaaa-0000-0000-0000-000000000001","openedAt":"2026-11-02T09:00:00Z","messages":[]}
            """

        #expect(ChatThreadAPI.reply(.init(name: "delta", data: #"{"text":"Rest today."}"#)) == .wrote("Rest today."))
        #expect(ChatThreadAPI.reply(.init(name: "tools", data: #"{"tools":["GetTrainingScore","SearchMemory"]}"#))
            == .consulting(tools: ["GetTrainingScore", "SearchMemory"]))
        #expect(ChatThreadAPI.reply(.init(name: "done", data: session))
            == .stored(try Decoding.decode(ThreadSession.self, session)))
        #expect(ChatThreadAPI.reply(.init(
            name: "failed", data: #"{"status":502,"title":"An upstream service failed.","detail":"Anthropic is unavailable."}"#))
            == .refused(.upstream(detail: "Anthropic is unavailable.")))
    }

    @Test("An event this build does not know, or cannot read, is passed over rather than ending the answer")
    func unreadableRepliesArePassedOver() {
        #expect(ChatThreadAPI.reply(.init(name: "usage", data: "{}")) == .unrecognised(name: "usage"))
        #expect(ChatThreadAPI.reply(.init(name: "delta", data: "not json")) == .unrecognised(name: "delta"))
    }

    // MARK: - The answer so far

    @Test("Pieces of the answer join in order")
    func writingAccumulates() {
        let progress = ReplyProgress.thinking
            .after(.wrote("Rest "))
            .after(.wrote("today."))

        #expect(progress == .writing("Rest today."))
        #expect(progress.written == "Rest today.")
    }

    @Test("A round of tools withdraws what was written before it, and the answer starts again after")
    func toolsWithdrawThePreamble() {
        let consulting = ReplyProgress.thinking
            .after(.wrote("Let me look."))
            .after(.consulting(tools: ["GetTrainingScore"]))

        #expect(consulting == .consulting(tools: ["GetTrainingScore"]))
        #expect(consulting.written == nil)
        #expect(consulting.after(.wrote("Train.")) == .writing("Train."))
    }

    @Test("Tool names read as what is being read")
    func toolNamesRead() {
        #expect(ReplyProgress.reading(["GetTrainingScore"]) == "training score")
        #expect(ReplyProgress.reading(["SearchMemory", "GetOuraSleepNights"]).contains("Oura sleep nights"))
        #expect(ReplyProgress.reading(["SearchMemory", "GetOuraSleepNights"]).hasPrefix("memory"))
    }

    // MARK: - Finding the answer after the connection went

    private static func thread(_ messages: [String]) throws -> ChatThread {
        try Decoding.decode(ChatThread.self, """
            {"sessions":[{"sessionId":"aaaaaaaa-0000-0000-0000-000000000001","openedAt":"2026-03-04T17:00:00Z",
              "messages":[\(messages.joined(separator: ","))]}],"hasEarlier":false}
            """)
    }

    private static let askedAt = ISO8601DateFormatter().date(from: "2026-03-04T17:29:00Z") ?? .distantPast

    @Test("The answer is the model's turn after the question, once the thread holds both")
    func findingTheAnswer() throws {
        let answered = try Self.thread([
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "Legs?", createdAt: "2026-03-04T17:30:00"),
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "Squat.", createdAt: "2026-03-04T17:31:00"),
        ])
        let unanswered = try Self.thread([
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "Legs?", createdAt: "2026-03-04T17:30:00"),
        ])

        #expect(ThreadTranscript.answer(to: "Legs?", askedAt: Self.askedAt, in: answered)?.text == "Squat.")
        #expect(ThreadTranscript.answer(to: "Legs?", askedAt: Self.askedAt, in: unanswered) == nil)
        #expect(ThreadTranscript.answer(to: "Arms?", askedAt: Self.askedAt, in: answered) == nil)
    }

    @Test("The same words asked earlier are not this question's answer")
    func anEarlierAskingDoesNotCount() throws {
        let earlier = try Self.thread([
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "Legs?", createdAt: "2026-03-04T17:00:00"),
            Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "Squat.", createdAt: "2026-03-04T17:01:00"),
        ])

        #expect(ThreadTranscript.answer(to: "Legs?", askedAt: Self.askedAt, in: earlier) == nil)
    }
}
