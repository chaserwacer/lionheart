import Foundation
import Testing

@testable import Lionheart

/// How a day reads next to today.
///
/// `today` is injected in every case, so none of these can fail on a particular date.
@Suite("Relative day")
struct RelativeDayTests {
    private static let today = CalendarDay(year: 2026, month: 3, day: 4)

    @Test(
        "Nearby days read as words and distant ones as a date",
        arguments: [
            (CalendarDay(year: 2026, month: 3, day: 4), RelativeDay.today),
            (CalendarDay(year: 2026, month: 3, day: 5), .tomorrow),
            (CalendarDay(year: 2026, month: 3, day: 3), .yesterday),
            (CalendarDay(year: 2026, month: 3, day: 6), .inDays(2)),
            (CalendarDay(year: 2026, month: 3, day: 2), .daysAgo(2)),
            (CalendarDay(year: 2026, month: 3, day: 10), .inDays(6)),
            (CalendarDay(year: 2026, month: 2, day: 26), .daysAgo(6)),
        ])
    func nearbyDaysReadAsWords(day: CalendarDay, expected: RelativeDay) {
        #expect(RelativeDay.of(day, today: Self.today) == expected)
    }

    @Test("Beyond a week either side, the date itself reads better than a count")
    func distantDaysReadAsDates() {
        #expect(RelativeDay.of(CalendarDay(year: 2026, month: 3, day: 11), today: Self.today)
            == .distant(CalendarDay(year: 2026, month: 3, day: 11)))
        #expect(RelativeDay.of(CalendarDay(year: 2026, month: 2, day: 25), today: Self.today)
            == .distant(CalendarDay(year: 2026, month: 2, day: 25)))
    }

    @Test("A month boundary is counted in days, not in month arithmetic")
    func monthBoundariesAreCountedInDays() {
        let today = CalendarDay(year: 2026, month: 3, day: 1)

        #expect(RelativeDay.of(CalendarDay(year: 2026, month: 2, day: 28), today: today) == .yesterday)
        #expect(RelativeDay.of(CalendarDay(year: 2026, month: 2, day: 27), today: today) == .daysAgo(2))
    }
}

/// Choosing words for a value. Presentation, so it lives in `Support/` — but the pluralisation and
/// the clock are easy to get subtly wrong, and both appear on the workout bar.
@Suite("Formatting")
struct FormattingTests {
    @Test(
        "A clock shows hours only once there are any",
        arguments: [(0.0, "0:00"), (9.0, "0:09"), (72.0, "1:12"), (2_412.0, "40:12"), (3_600.0, "1:00:00"),
                    (4_332.0, "1:12:12")])
    func aClockShowsHoursOnlyWhenItHasThem(seconds: Double, expected: String) {
        #expect(Formatting.clock(seconds: seconds) == expected)
    }

    @Test("A negative elapsed time reads as zero rather than as a minus sign")
    func aNegativeClockReadsAsZero() {
        #expect(Formatting.clock(seconds: -30) == "0:00")
    }

    @Test(
        "A duration reads in the units a person would say",
        arguments: [(2_880.0, "48 min"), (3_600.0, "1 h"), (4_320.0, "1 h 12 min"), (7_200.0, "2 h"),
                    (30.0, "1 min")])
    func aDurationReadsInWords(seconds: Double, expected: String) {
        #expect(Formatting.duration(seconds: seconds) == expected)
    }

    @Test("A count of one is singular")
    func oneIsSingular() {
        #expect(Formatting.count(1, "set") == "1 set")
        #expect(Formatting.count(0, "set") == "0 sets")
        #expect(Formatting.count(3, "set") == "3 sets")
        #expect(Formatting.count(2, "prescription") == "2 prescriptions")
    }

    @Test("A rep max reads at its own count and says when it was the front of a longer set")
    func aCarriedRepMaxSaysWhichSet() {
        let fromSeven = Achievement.repMax(
            reps: 6, .init(weight: 180, unit: .kilograms, reps: 7, estimatedOneRepMax: 222))
        let cleanSix = Achievement.repMax(
            reps: 6, .init(weight: 185, unit: .kilograms, reps: 6, estimatedOneRepMax: 222))

        // Logged in kilograms, read in pounds: 180 kg is 396.8 lb, an estimated 222 kg is 489 lb.
        #expect(Formatting.achievement(fromSeven) == "396.8 lb × 6")
        #expect(Formatting.achievementDetail(fromSeven) == "From a set of 7 · estimated single ≈ 489 lb")
        #expect(Formatting.achievementDetail(cleanSix) == "Estimated single ≈ 489 lb")
    }

    @Test("A set that beat several records says so in one sentence, not a list")
    func recordsBeatenIsOneSentence() {
        #expect(Formatting.recordsBeaten(.beats(.repMax(reps: 5), also: [])) == "New 5-rep max")
        #expect(Formatting.recordsBeaten(.beats(.repMax(reps: 5), also: [.bestSetVolume]))
            == "New 5-rep max and 1 more record")
        #expect(Formatting.recordsBeaten(.none) == nil)
        #expect(Formatting.recordsBeaten(.noRecordYet) == nil)
    }
}

/// Where a quick-log entry stands.
@Suite("Quick log state")
struct QuickLogStateTests {
    @Test("Committing names what was written")
    func committingNamesWhatWasWritten() {
        let state = QuickLogState.after(.committed(InterpretationOutcome.Committed(
            sessionIds: [UUID()], contextIds: [UUID(), UUID()], warnings: ["low confidence"])))

        #expect(state == .committed(sessions: 1, contexts: 2, warnings: ["low confidence"]))
        #expect(state.hasOutcome)
        #expect(!state.isInterpreting)
    }

    @Test("Needing a confirmation is an outcome, not a refusal")
    func needingAConfirmationIsNotARefusal() {
        let reference = UnresolvedReference(rawName: "sled push", kind: .movementBase, candidates: [])

        let state = QuickLogState.after(.awaitingConfirmation(
            InterpretationOutcome.AwaitingConfirmation(references: [reference], draft: .null)))

        #expect(state == .needsConfirmation([reference]))
        #expect(state.hasOutcome)
    }

    @Test("Only the interpreting state reports that it is interpreting")
    func interpretingIsOneState() {
        #expect(QuickLogState.interpreting.isInterpreting)
        #expect(!QuickLogState.idle.isInterpreting)
        #expect(!QuickLogState.committed(sessions: 1, contexts: 0, warnings: []).isInterpreting)
        #expect(!QuickLogState.refused(.notFound).isInterpreting)
    }

    @Test("Idle and interpreting have nothing to show")
    func onlyFinishedStatesHaveAnOutcome() {
        #expect(!QuickLogState.idle.hasOutcome)
        #expect(!QuickLogState.interpreting.hasOutcome)
        #expect(QuickLogState.refused(.notFound).hasOutcome)
    }
}

/// Whether a set of typed credentials is worth sending.
@Suite("Credentials")
struct CredentialsTests {
    @Test("An empty form is not submittable")
    func anEmptyFormIsNotSubmittable() {
        #expect(!Credentials.areSubmittable(email: "", password: ""))
        #expect(!Credentials.areSubmittable(email: "a@b.com", password: ""))
        #expect(!Credentials.areSubmittable(email: "", password: "hunter2"))
        #expect(!Credentials.areSubmittable(email: "   ", password: "hunter2"))
    }

    @Test("Anything the server might accept is submittable")
    func theClientDoesNotSecondGuessTheServer() {
        // The server owns what a valid password is. A client that enforced its own rules would
        // refuse credentials the server would have taken.
        #expect(Credentials.areSubmittable(email: "a@b.com", password: "x"))
        #expect(Credentials.areSubmittable(email: "not-an-email", password: "x"))
    }
}

/// The token set's freshness rule.
@Suite("Token freshness")
struct TokenSetTests {
    private static func tokens(expiringIn seconds: TimeInterval) -> TokenSet {
        TokenSet(
            accessToken: "access",
            refreshToken: "refresh",
            expiresAt: Date(timeIntervalSince1970: 1000 + seconds))
    }

    private static let now = Date(timeIntervalSince1970: 1000)

    @Test("A token with plenty of life left is fresh")
    func aLivelyTokenIsFresh() {
        #expect(Self.tokens(expiringIn: 3600).isFresh(at: Self.now))
    }

    @Test("A token about to expire is not fresh, because expiry is spent on the server")
    func aTokenInsideTheMarginIsNotFresh() {
        // Checked on the device, spent on the server: a token with two seconds left will already
        // be expired by the time the request lands.
        #expect(!Self.tokens(expiringIn: 2).isFresh(at: Self.now))
        #expect(!Self.tokens(expiringIn: 30).isFresh(at: Self.now))
        #expect(Self.tokens(expiringIn: 31).isFresh(at: Self.now))
    }

    @Test("An expired token is not fresh")
    func anExpiredTokenIsNotFresh() {
        #expect(!Self.tokens(expiringIn: -1).isFresh(at: Self.now))
    }

    @Test("Expiry is derived at receipt, because a relative lifetime stops meaning anything on disk")
    func expiryIsDerivedAtReceipt() throws {
        let response = try Decoding.decode(
            IdentityTokenResponse.self,
            #"{"tokenType":"Bearer","accessToken":"a","refreshToken":"r","expiresIn":3600}"#)

        let tokens = response.tokens(receivedAt: Self.now)

        #expect(tokens.accessToken == "a")
        #expect(tokens.refreshToken == "r")
        #expect(tokens.expiresAt == Date(timeIntervalSince1970: 4600))
    }

    @Test("A sign-in response with no tokens in it is refused")
    func aResponseWithoutTokensIsRefused() {
        // Sending useCookies would produce exactly this: a 200 whose body has no tokens.
        #expect(throws: APIError.self) {
            try Decoding.decode(IdentityTokenResponse.self, #"{"tokenType":"Bearer"}"#)
        }
    }
}
