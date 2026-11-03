import Foundation
import Testing

@testable import Lionheart

/// The rule the load-state enum exists to enforce: a failed refresh never blanks a value already
/// on screen.
///
/// It is the most load-bearing derivation in the client. Pull to refresh in a basement is the
/// common case, and the wrong answer — an empty screen because the network dropped — is both the
/// easy thing to write and the thing an athlete notices immediately.
@Suite("Load state")
struct LoadStateTests {
    private typealias State = LoadState<Int>

    @Test("A failed refresh keeps the value and says why it is stale")
    func aFailedRefreshKeepsTheValue() {
        let loaded = State.loaded(7)

        let after = loaded.applying(.failure(.offline("no route to host")))

        #expect(after == .stale(7, .offline("no route to host")))
        #expect(after.value == 7)
    }

    @Test("A second failed refresh keeps the same value and replaces only the reason")
    func repeatedFailuresDoNotErodeTheValue() {
        let stale = State.stale(7, .offline("first"))

        let after = stale.applying(.failure(.unavailable("second")))

        #expect(after.value == 7)
        #expect(after.failure == .unavailable("second"))
    }

    @Test("A first attempt that fails has nothing to keep, so it is unavailable")
    func aFirstFailureIsUnavailable() {
        let after = State.loading.applying(.failure(.notFound))

        #expect(after == .unavailable(.notFound))
        #expect(after.value == nil)
    }

    @Test("A failure after an earlier failure stays unavailable")
    func failingTwiceWithoutEverLoadingStaysUnavailable() {
        let after = State.unavailable(.notFound).applying(.failure(.offline("still down")))

        #expect(after == .unavailable(.offline("still down")))
    }

    @Test("A success replaces whatever was there, including a stale value")
    func aSuccessClearsStaleness() {
        #expect(State.loading.applying(.success(1)) == .loaded(1))
        #expect(State.stale(7, .offline("x")).applying(.success(9)) == .loaded(9))
        #expect(State.unavailable(.notFound).applying(.success(3)) == .loaded(3))
        #expect(State.stale(7, .offline("x")).applying(.success(9)).failure == nil)
    }

    @Test("Only the states that hold a value answer with one")
    func valueIsAnsweredByExactlyTheStatesThatHaveOne() {
        #expect(State.loading.value == nil)
        #expect(State.unavailable(.notFound).value == nil)
        #expect(State.loaded(4).value == 4)
        #expect(State.stale(4, .notFound).value == 4)
    }

    @Test("Only the states that failed answer with a reason")
    func failureIsAnsweredByExactlyTheStatesThatFailed() {
        #expect(State.loading.failure == nil)
        #expect(State.loaded(4).failure == nil)
        #expect(State.unavailable(.notFound).failure == .notFound)
        #expect(State.stale(4, .offline("x")).failure == .offline("x"))
    }
}

/// Restating a network failure as a feature's own vocabulary, once, at the boundary.
@Suite("Failure restatement")
struct FailureRestatementTests {
    @Test("Cancellation is rethrown rather than classified")
    func cancellationNeverBecomesADomainFailure() {
        // A dismissed screen is not a failure. Classifying cancellation is how a navigation pop
        // turns into a red banner, which is why this throws instead of returning a case.
        #expect(throws: CancellationError.self) { try LoadFailure.from(CancellationError()) }
        #expect(throws: CancellationError.self) { try SignInFailure.from(CancellationError()) }
    }

    @Test(
        "Each HTTP problem becomes the one load failure that matches it",
        arguments: [
            (APIError.transport("offline"), LoadFailure.offline("offline")),
            (.notFound(detail: "gone"), .notFound),
            (.refused(detail: "already completed"), .refused("already completed")),
            (.upstream(detail: nil), .unavailable("A service Lionheart depends on is not responding.")),
            (.unauthenticated, .unavailable("You are not signed in.")),
        ])
    func anApiErrorBecomesItsMatchingLoadFailure(error: APIError, expected: LoadFailure) throws {
        #expect(try LoadFailure.from(error) == expected)
    }

    @Test("A 404 reads as not found, because the server answers it for another athlete's record")
    func notFoundDoesNotInventAMeaning() throws {
        // Ownership is checked on every route and a stranger's session is deliberately
        // indistinguishable from one that does not exist. The client must not translate that into
        // "forbidden" and tell the athlete something the server refused to.
        #expect(try LoadFailure.from(APIError.notFound(detail: nil)) == .notFound)
        #expect(LoadFailure.notFound.isWorthRetrying == false)
    }

    @Test("A 409 is a refusal worth stating and not worth retrying")
    func aConflictIsStatedNotRetried() throws {
        let failure = try LoadFailure.from(APIError.refused(detail: "Session is already completed."))

        #expect(failure.message == "Session is already completed.")
        #expect(failure.isWorthRetrying == false)
    }

    @Test("A wrong password and an unknown email are the same sign-in refusal")
    func identityDoesNotDiscloseWhichAddressesExist() throws {
        #expect(try SignInFailure.from(APIError.unauthenticated) == .credentialsRejected)
        #expect(try SignInFailure.from(APIError.forbidden) == .credentialsRejected)
    }

    @Test("A validation problem is reported in the server's own words")
    func aValidationProblemCarriesItsFieldMessage() throws {
        let error = APIError.invalid(
            fields: ["Email": ["Email is not confirmed."]], detail: "One or more errors occurred.")

        #expect(try SignInFailure.from(error) == .accountNotReady("Email is not confirmed."))
    }

    @Test("Only an answer about the credential discards it")
    func aBlipDoesNotDiscardTheCredential() {
        // A renewal that failed on a timeout or a 502 says nothing about whether the refresh token
        // is still good. Discarding it for one of those signs the athlete out over a blip.
        #expect(APIError.unauthenticated.rejectsCredential)
        #expect(APIError.forbidden.rejectsCredential)
        #expect(APIError.invalid(fields: [:], detail: nil).rejectsCredential)

        #expect(!APIError.transport("timed out").rejectsCredential)
        #expect(!APIError.upstream(detail: nil).rejectsCredential)
        #expect(!APIError.server(detail: nil).rejectsCredential)
        #expect(!APIError.decoding("unreadable body").rejectsCredential)
        #expect(!APIError.notFound(detail: nil).rejectsCredential)
        #expect(!APIError.refused(detail: nil).rejectsCredential)
        #expect(!APIError.http(status: 418, detail: nil).rejectsCredential)
    }

    @Test("Only the failures a retry could fix classify as transient")
    func transienceIsTotalOverTheCases() {
        #expect(APIError.transport("x").isTransient)
        #expect(APIError.upstream(detail: nil).isTransient)
        #expect(APIError.server(detail: nil).isTransient)
        #expect(!APIError.unauthenticated.isTransient)
        #expect(!APIError.notFound(detail: nil).isTransient)
        #expect(!APIError.refused(detail: nil).isTransient)
        #expect(!APIError.invalid(fields: [:], detail: nil).isTransient)
        #expect(!APIError.decoding("x").isTransient)
    }
}

/// How the server's RFC 7807 bodies become `APIError`s.
@Suite("Problem documents")
struct ProblemDocumentTests {
    @Test("A 400 carries its field problems, keyed by field")
    func aValidationProblemKeepsItsFields() throws {
        let body = Data(
            #"{"title":"One or more validation errors occurred.","status":400,"errors":{"StartDate":["StartDate must be the same as, or come before, EndDate."]}}"#
                .utf8)

        guard case .invalid(let fields, let detail) = HTTP.failure(status: 400, body: body) else {
            Issue.record("Expected .invalid")
            return
        }
        #expect(fields["StartDate"] == ["StartDate must be the same as, or come before, EndDate."])
        #expect(detail == "One or more validation errors occurred.")
    }

    @Test("A problem document's detail is preferred over its title")
    func detailIsWhatTheAthleteReads() throws {
        let body = Data(
            #"{"title":"There was a conflict.","status":409,"detail":"Session is already completed."}"#
                .utf8)

        #expect(HTTP.failure(status: 409, body: body)
            == .refused(detail: "Session is already completed."))
    }

    @Test("A status with no problem body still becomes a failure a caller can see")
    func anEmptyBodyStillFails() {
        #expect(HTTP.failure(status: 418, body: Data()) == .http(status: 418, detail: nil))
        #expect(HTTP.failure(status: 401, body: Data()) == .unauthenticated)
        #expect(HTTP.failure(status: 403, body: Data()) == .forbidden)
    }

    @Test("A 2xx is not a failure")
    func successIsNotAFailure() {
        #expect(HTTP.failure(status: 200, body: Data()) == nil)
        #expect(HTTP.failure(status: 201, body: Data()) == nil)
        #expect(HTTP.failure(status: 204, body: Data()) == nil)
    }
}
