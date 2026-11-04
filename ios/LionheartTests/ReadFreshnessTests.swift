import Foundation
import Testing

@testable import Lionheart

/// When a screen coming back into view reads again, and when the device's last answer is painted
/// while it does.
@Suite("Read freshness")
struct ReadFreshnessTests {
    typealias State = LoadState<Int>

    private static let calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    /// Mid-morning, so a few minutes either way stays on the same day.
    private static let readAt = Date(timeIntervalSince1970: 1_772_618_400)
    private static let stamp = ReadStamp(readAt: readAt, acceptedWrites: 3)
    private static let freshFor: TimeInterval = 300

    private func needsRead(
        _ state: State = .loaded(1), lastRead: ReadStamp? = stamp, after seconds: TimeInterval,
        writes: Int = 3
    ) -> Bool {
        ReadFreshness.needsRead(
            state, lastRead: lastRead, now: Self.readAt.addingTimeInterval(seconds),
            acceptedWrites: writes, freshFor: Self.freshFor, calendar: Self.calendar)
    }

    @Test("A current answer read moments ago is not read again")
    func aFreshAnswerStands() {
        #expect(!needsRead(after: 20))
    }

    @Test("A write since the read makes the screen read again, however recent it is")
    func aWriteSinceTheReadIsDue() {
        #expect(needsRead(after: 1, writes: 4))
    }

    @Test("An answer older than its span is read again")
    func anOldAnswerIsDue() {
        #expect(!needsRead(after: Self.freshFor - 1))
        #expect(needsRead(after: Self.freshFor))
    }

    @Test("A new calendar day reads again, even inside the span")
    func aNewDayIsDue() {
        let lateEvening = ReadStamp(
            readAt: Date(timeIntervalSince1970: 1_772_668_740), acceptedWrites: 3)  // 23:59 UTC
        #expect(needsRead(lastRead: lateEvening, after: 1_772_668_800 - 1_772_618_400))  // 00:00
    }

    @Test("A clock set back reads again rather than trusting an answer from the future")
    func aClockSetBackIsDue() {
        #expect(needsRead(after: -60))
    }

    @Test("A screen with nothing current on it always reads", arguments: [
        State.loading,
        State.unavailable(.offline("x")),
        State.stale(1, .unreachable(asOf: readAt)),
        State.stale(1, .refreshFailed(.notFound)),
        State.stale(1, .recalled(asOf: readAt)),
    ])
    func notCurrentIsDue(state: State) {
        #expect(needsRead(state, after: 1))
    }

    @Test("A loaded screen with no record of its read reads")
    func noStampIsDue() {
        #expect(needsRead(lastRead: nil, after: 1))
    }

    // MARK: - Recalling

    @Test("A first load paints the recalled answer, marked as recalled")
    func aFirstLoadTakesTheRecalledAnswer() {
        let painted = State.loading.recalling(7, asOf: Self.readAt)

        #expect(painted == .stale(7, .recalled(asOf: Self.readAt)))
        #expect(painted.recalledAsOf == Self.readAt)
        // Waiting, not failing: there is nothing to tell the athlete went wrong.
        #expect(painted.failure == nil)
        #expect(painted.isWaitingOnServer)
    }

    @Test("A screen already showing a value is never stepped back to the device's copy")
    func aValueOnScreenIsKept() {
        #expect(State.loaded(9).recalling(7, asOf: Self.readAt) == .loaded(9))
        let held = State.stale(9, .unreachable(asOf: Self.readAt))
        #expect(held.recalling(7, asOf: Self.readAt) == held)
    }

    @Test("The server's answer replaces a recalled one; its silence marks it unreachable")
    func theServerSettlesARecalledScreen() {
        let painted = State.loading.recalling(7, asOf: Self.readAt)

        #expect(painted.applying(Remembered.current(9)) == .loaded(9))
        #expect(painted.applying(Remembered.recalled(7, asOf: Self.readAt))
            == .stale(7, .unreachable(asOf: Self.readAt)))
        #expect(painted.applying(.failure(.notFound)) == .stale(7, .refreshFailed(.notFound)))
        #expect(painted.applying(Remembered.current(9)).recalledAsOf == nil)
    }
}
