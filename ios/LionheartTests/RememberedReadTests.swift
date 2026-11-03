import Foundation
import Testing

@testable import Lionheart

/// What a screen shows when the server cannot be reached: what it last knew, marked with when.
@Suite("Remembered reads")
struct RememberedReadTests {
    private typealias State = LoadState<Int>

    private static let morning = Date(timeIntervalSince1970: 1_772_600_000)
    private static let evening = morning.addingTimeInterval(10 * 3600)

    @Test("A recalled answer is shown, marked with the time it dates from")
    func aRecalledAnswerIsStaleAsOfItsTime() {
        let after = State.loading.applying(Remembered.recalled(7, asOf: Self.morning))

        #expect(after == .stale(7, .unreachable(asOf: Self.morning)))
        #expect(after.value == 7)
    }

    @Test("A current answer replaces a recalled one")
    func aCurrentAnswerClearsTheMark() {
        let recalled = State.stale(7, .unreachable(asOf: Self.morning))

        #expect(recalled.applying(Remembered.current(9)) == .loaded(9))
    }

    @Test("A screen built from parts is current only when every part is")
    func everyPartCurrentIsCurrent() {
        #expect(Remembered.derived(3, from: [nil, nil, nil]) == .current(3))
    }

    @Test("A screen built from parts is as old as its oldest recalled part")
    func theOldestPartDatesTheScreen() {
        let derived = Remembered.derived(3, from: [nil, Self.evening, Self.morning])

        // One part current and two recalled is a connection that dropped between requests; the
        // screen still is not current, and it is as old as the morning answer.
        #expect(derived == .recalled(3, asOf: Self.morning))
    }

    @Test("Only a value on screen that is not current counts as stale")
    func stalenessIsTheStaleCaseOnly() {
        #expect(State.stale(1, .unreachable(asOf: Self.morning)).isStale)
        #expect(State.stale(1, .refreshFailed(.notFound)).isStale)
        #expect(!State.loaded(1).isStale)
        #expect(!State.loading.isStale)
        #expect(!State.unavailable(.offline("x")).isStale)
    }
}

/// The marker's words, which have to read like an instrument's and never let an old reading pass
/// for a recent one.
@Suite("As-of marker")
struct AsOfMarkerTests {
    private static let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    private static func at(_ day: Int, _ hour: Int, _ minute: Int) -> Date {
        calendar.date(from: DateComponents(year: 2026, month: 11, day: day, hour: hour, minute: minute))!
    }

    @Test("Today's answer is marked with its time alone")
    func todayIsTheTime() {
        let marker = Formatting.asOf(Self.at(3, 14, 2), now: Self.at(3, 18, 0), calendar: Self.calendar)

        #expect(marker.hasPrefix("As of "))
        #expect(!marker.localizedCaseInsensitiveContains("yesterday"))
        #expect(!marker.contains("Nov"))
    }

    @Test("Yesterday's answer says so")
    func yesterdayIsNamed() {
        let marker = Formatting.asOf(Self.at(2, 18, 40), now: Self.at(3, 9, 0), calendar: Self.calendar)

        #expect(marker.hasPrefix("As of yesterday "))
    }

    @Test("An older answer carries its date")
    func olderCarriesTheDate() {
        let marker = Formatting.asOf(Self.at(1, 7, 0), now: Self.at(3, 9, 0), calendar: Self.calendar)

        #expect(marker.contains("Nov"))
    }
}

/// The device's store of last answers, per account.
@Suite("Read memory")
struct ReadMemoryTests {
    private static func aMemory() -> ReadMemory {
        ReadMemory(root: URL.temporaryDirectory.appending(path: UUID().uuidString))
    }

    private static let asOf = Date(timeIntervalSince1970: 1_772_600_000)

    @Test("An answer kept is the answer recalled, with its time")
    func anAnswerRoundTrips() throws {
        let memory = Self.aMemory()
        let account = memory.currentAccount()

        memory.remember(Data("[1,2]".utf8), as: .todaySessions, for: account, at: Self.asOf)

        let recalled = try #require(memory.recall(.todaySessions, for: account))
        #expect(recalled.body == Data("[1,2]".utf8))
        #expect(recalled.asOf == Self.asOf)
    }

    @Test("Each read keeps its own answer")
    func readsDoNotShareAnswers() {
        let memory = Self.aMemory()
        let account = memory.currentAccount()

        memory.remember(Data("today".utf8), as: .todaySessions, for: account, at: Self.asOf)

        #expect(memory.recall(.historySessions, for: account) == nil)
    }

    @Test("Signing out forgets every answer, and the next account starts with none")
    func forgettingIsTotal() {
        let memory = Self.aMemory()
        let first = memory.currentAccount()
        memory.remember(Data("first".utf8), as: .todaySessions, for: first, at: Self.asOf)

        memory.forget()
        let next = memory.currentAccount()

        #expect(next != first)
        #expect(memory.recall(.todaySessions, for: next) == nil)
    }

    @Test("A request that lands after a sign-out keeps nothing")
    func aLateAnswerForAGoneAccountIsDropped() {
        // The answer was asked for by the account that has since signed out; keeping it would put
        // that athlete's data in front of the next one.
        let memory = Self.aMemory()
        let gone = memory.currentAccount()
        memory.forget()
        let next = memory.currentAccount()

        memory.remember(Data("late".utf8), as: .todaySessions, for: gone, at: Self.asOf)

        #expect(memory.recall(.todaySessions, for: next) == nil)
        #expect(memory.recall(.todaySessions, for: gone) == nil)
    }

    @Test("The account survives a relaunch, so its answers do too")
    func theAccountIsPersisted() {
        let root = URL.temporaryDirectory.appending(path: UUID().uuidString)
        let first = ReadMemory(root: root)
        let account = first.currentAccount()
        first.remember(Data("kept".utf8), as: .profileBoot, for: account, at: Self.asOf)

        let relaunched = ReadMemory(root: root)

        #expect(relaunched.currentAccount() == account)
        #expect(relaunched.recall(.profileBoot, for: account)?.body == Data("kept".utf8))
    }

    @Test("A key built from a record's identity cannot name a path outside its directory")
    func keysAreFileNames() {
        #expect(ReadKey("../../escape/x").fileName == "..-..-escape-x")
        #expect(!ReadKey("records.history.a/b").fileName.contains("/"))
    }
}

/// Whether the server is answering, learned from the requests the app makes.
@MainActor
@Suite("Server reachability")
struct ServerReachabilityTests {
    @Test("The server's return is announced once, when it answers after not answering")
    func aReturnIsAnnouncedOnce() {
        let reachability = ServerReachability()
        reachability.leftForeground()
        var returns = 0
        reachability.onReturn { returns += 1 }

        reachability.record(answered: false)
        reachability.record(answered: false)
        reachability.record(answered: true)
        reachability.record(answered: true)

        #expect(returns == 1)
        #expect(reachability.isReachable)
    }

    @Test("A server that never went away is never announced as back")
    func noReturnWithoutAnAbsence() {
        let reachability = ServerReachability()
        var returns = 0
        reachability.onReturn { returns += 1 }

        reachability.record(answered: true)

        #expect(returns == 0)
    }

    @Test("A request that never completed marks the server unreachable")
    func anUnansweredRequestIsUnreachable() {
        let reachability = ServerReachability()
        reachability.leftForeground()

        reachability.record(answered: false)

        #expect(!reachability.isReachable)
    }
}

/// The gateway in front of the server answering for it when the server is not running.
@Suite("Gateway stand-in")
struct GatewayStandInTests {
    @Test("A gateway status with no problem document is the server not being there")
    func theGatewaysOwnAnswerIsUnreachable() {
        let body = Data("Bad Gateway".utf8)

        #expect(HTTP.isGatewayStandIn(status: 502, body: body))
        guard case .transport? = HTTP.failure(status: 502, body: body) else {
            Issue.record("expected a transport failure")
            return
        }
    }

    @Test("The server's own 502, an upstream failing, is still the server's answer")
    func theServersOwn502IsAnAnswer() {
        let body = Data(#"{"title":"Upstream failure","detail":"Oura did not answer.","status":502}"#.utf8)

        #expect(!HTTP.isGatewayStandIn(status: 502, body: body))
        #expect(HTTP.failure(status: 502, body: body) == .upstream(detail: "Oura did not answer."))
    }
}

/// Waiting for a start, but not for ever.
@Suite("Deadline")
struct DeadlineTests {
    private struct Expired: Error {}

    @Test("An answer inside the limit is the answer")
    func anAnswerInTimeWins() async throws {
        let answer = try await Deadline.waiting(atMost: .seconds(5), expiring: { Expired() }) { 42 }

        #expect(answer == 42)
    }

    @Test("No answer inside the limit throws the expiry")
    func silenceExpires() async {
        await #expect(throws: Expired.self) {
            try await Deadline.waiting(atMost: .milliseconds(50), expiring: { Expired() }) {
                try await Task.sleep(for: .seconds(30))
                return 1
            }
        }
    }
}
