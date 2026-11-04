import Foundation
import Testing

@testable import Lionheart

/// The continuing thread as the screen reads it: sessions in order, each opening a seam, the
/// athlete's unsaved turn at the bottom, and earlier stretches put in front without repeats.
@Suite("Chat thread")
struct ChatThreadTests {
    private static func session(_ id: String, openedAt: String, messages: String = "[]") -> String {
        """
        {"sessionId":"\(id)","openedAt":"\(openedAt)","messages":\(messages)}
        """
    }

    private static let first = "aaaaaaaa-0000-0000-0000-000000000001"
    private static let second = "aaaaaaaa-0000-0000-0000-000000000002"
    private static let third = "aaaaaaaa-0000-0000-0000-000000000003"

    private static func thread(_ sessions: [String], hasEarlier: Bool = false) throws -> ChatThread {
        try Decoding.decode(ChatThread.self, """
            {"sessions":[\(sessions.joined(separator: ","))],"hasEarlier":\(hasEarlier)}
            """)
    }

    @Test("A page decodes oldest session first, whatever order it arrives in")
    func sessionsAreOldestFirst() throws {
        let thread = try Self.thread([
            Self.session(Self.second, openedAt: "2026-11-03T18:00:00Z"),
            Self.session(Self.first, openedAt: "2026-11-02T09:00:00Z"),
        ], hasEarlier: true)

        #expect(thread.sessions.map(\.sessionId.uuidString) == [Self.first.uppercased(), Self.second.uppercased()])
        #expect(thread.hasEarlier)
    }

    @Test("A session with no opening cannot be placed and is dropped, not the page")
    func aSessionWithoutAnOpeningIsDropped() throws {
        let thread = try Self.thread([
            """
            {"sessionId":"\(Self.first)","messages":[]}
            """,
            Self.session(Self.second, openedAt: "2026-11-03T18:00:00Z"),
        ])

        #expect(thread.sessions.count == 1)
    }

    @Test("Rows open each session with its seam, then its turns, then the unsaved turn")
    func rowsInterleaveOpeningsAndTurns() throws {
        let thread = try Self.thread([
            Self.session(Self.first, openedAt: "2026-11-02T09:00:00Z", messages: """
                [\(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "Knee hurts")),
                 \(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02", role: 1, content: "Noted",
                    createdAt: "2026-03-04T17:31:00")),
                 \(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03", role: 2, content: "{tool}"))]
                """),
            Self.session(Self.second, openedAt: "2026-11-03T18:00:00Z"),
        ])
        let pending = ChatTurn.pending(id: UUID(), text: "Legs today?")

        let rows = ThreadTranscript.rows(thread, tail: pending)

        #expect(rows.count == 5)
        guard case .opened = rows[0], case .turn(let asked) = rows[1], case .turn(let answered) = rows[2],
              case .opened = rows[3], case .turn(let unsaved) = rows[4]
        else {
            Issue.record("Rows were \(rows)")
            return
        }
        #expect(asked.text == "Knee hurts")
        #expect(answered.text == "Noted")
        #expect(unsaved == pending)
    }

    @Test("An earlier stretch goes in front, keeps its own hasEarlier, and repeats no session")
    func prependingEarlierSessions() throws {
        let newest = try Self.thread([
            Self.session(Self.second, openedAt: "2026-11-02T09:00:00Z"),
            Self.session(Self.third, openedAt: "2026-11-03T18:00:00Z"),
        ], hasEarlier: true)
        let earlier = try Self.thread([
            Self.session(Self.first, openedAt: "2026-11-01T09:00:00Z"),
            Self.session(Self.second, openedAt: "2026-11-02T09:00:00Z"),
        ], hasEarlier: false)

        let joined = newest.prepending(earlier)

        #expect(joined.sessions.count == 3)
        #expect(joined.oldestOpening == earlier.oldestOpening)
        #expect(!joined.hasEarlier)
    }

    @Test("An exchange's session replaces the one held, or is added when it opened a new one")
    func anExchangeUpdatesItsSession() throws {
        let thread = try Self.thread([Self.session(Self.first, openedAt: "2026-11-02T09:00:00Z")])
        let continued = try Decoding.decode(ThreadSession.self, Self.session(
            Self.first, openedAt: "2026-11-02T09:00:00Z",
            messages: "[\(Chat.message(id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01", role: 0, content: "More"))]"))
        let opened = try Decoding.decode(ThreadSession.self, Self.session(Self.second, openedAt: "2026-11-03T18:00:00Z"))

        #expect(thread.with(continued).sessions.map(\.messages.count) == [1])
        #expect(thread.with(opened).sessions.count == 2)
    }

    @Test("A refused exchange hands its words back; an awaited or recovering one holds the composer")
    func theExchangeStates() {
        let pending = ChatTurn.pending(id: UUID(), text: "Legs?")
        let unsent = ChatTurn.unsent(id: UUID(), text: "Legs?", failure: .offline("no route"))

        #expect(ThreadExchange.awaitingReply(pending, .thinking).isAwaitingReply)
        #expect(ThreadExchange.awaitingReply(pending, .writing("Squat")).unsentText == nil)
        #expect(ThreadExchange.recovering(pending).isAwaitingReply)
        #expect(ThreadExchange.recovering(pending).tail == pending)
        #expect(ThreadExchange.refused(unsent).unsentText == "Legs?")
        #expect(ThreadExchange.idle.tail == nil)
    }

    @Test("A session opening reads as today, yesterday, or its date")
    func openingLabels() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try #require(TimeZone(identifier: "UTC"))
        calendar.locale = Locale(identifier: "en_US")
        let now = try #require(ISO8601DateFormatter().date(from: "2026-11-03T20:00:00Z"))

        let today = ThreadTranscript.openingLabel(now.addingTimeInterval(-3600), now: now, calendar: calendar)
        let yesterday = ThreadTranscript.openingLabel(now.addingTimeInterval(-86_400), now: now, calendar: calendar)
        let lastWeek = ThreadTranscript.openingLabel(now.addingTimeInterval(-5 * 86_400), now: now, calendar: calendar)

        #expect(today.hasPrefix("Today"))
        #expect(yesterday.hasPrefix("Yesterday"))
        #expect(!lastWeek.hasPrefix("Today") && !lastWeek.hasPrefix("Yesterday"))
    }
}

/// What Lionheart remembers, as the memory screen groups and words it.
@Suite("Chat memory")
struct ChatMemoryTests {
    private static func memory(
        _ id: String, kind: Int, text: String = "Squat 405.", recordedAt: String = "2026-11-03T18:00:00",
        source: Int = 0
    ) -> String {
        """
        {"memoryId":"\(id)","kind":\(kind),"text":"\(text)","recordedAt":"\(recordedAt)","source":\(source),
         "quotes":[{"messageId":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01","byAthlete":true,"said":"My goal is 405.","at":"2026-11-03T17:59:00"}],
         "earlier":[{"kind":1,"text":"Knee sore.","recordedAt":"2026-10-20T10:00:00","replacedAt":"2026-11-03T18:00:00"}]}
        """
    }

    @Test("A memory decodes with its quotes and the versions it replaced")
    func decodesWhole() throws {
        let memory = try Decoding.decode(ChatMemory.self, Self.memory("aaaaaaaa-0000-0000-0000-000000000001", kind: 5))

        #expect(memory.kind == .fact)
        #expect(memory.source == .chat)
        #expect(memory.quotes.map(\.said) == ["My goal is 405."])
        #expect(memory.earlier.map(\.kind) == [.injury])
    }

    @Test("Sections follow the kinds' order, standing kinds first, and unknown kinds come last")
    func sectionsInKindOrder() throws {
        let memories = try Decoding.list(ChatMemory.self, """
            [\(Self.memory("aaaaaaaa-0000-0000-0000-000000000001", kind: 5)),
             \(Self.memory("aaaaaaaa-0000-0000-0000-000000000002", kind: 9)),
             \(Self.memory("aaaaaaaa-0000-0000-0000-000000000003", kind: 0)),
             \(Self.memory("aaaaaaaa-0000-0000-0000-000000000004", kind: 1))]
            """)

        let sections = MemorySection.of(memories)

        #expect(sections.map(\.title) == ["Goals", "Injuries", "Facts", "Other"])
        #expect(sections.prefix(2).allSatisfy { $0.kind.isStanding })
    }

    @Test("Newest first within a kind")
    func newestFirstWithinAKind() throws {
        let memories = try Decoding.list(ChatMemory.self, """
            [\(Self.memory("aaaaaaaa-0000-0000-0000-000000000001", kind: 0, text: "Older", recordedAt: "2026-10-01T00:00:00")),
             \(Self.memory("aaaaaaaa-0000-0000-0000-000000000002", kind: 0, text: "Newer", recordedAt: "2026-11-01T00:00:00"))]
            """)

        #expect(MemorySection.of(memories).first?.memories.map(\.text) == ["Newer", "Older"])
    }

    @Test("A kind round-trips through its wire value")
    func kindWire() {
        for kind in MemoryKind.known {
            #expect(MemoryKind(wire: kind.wire) == kind)
        }
        #expect(MemoryKind(wire: 42) == .unrecognised(42))
    }

    @Test("A memory says who wrote it")
    func provenanceNamesTheSource() throws {
        let corrected = try Decoding.decode(ChatMemory.self, Self.memory("aaaaaaaa-0000-0000-0000-000000000001", kind: 0, source: 1))

        #expect(MemoryWords.provenance(corrected).hasSuffix("corrected by you"))
        #expect(MemoryWords.quoteLine(corrected.quotes[0]).hasPrefix("You, "))
    }
}
