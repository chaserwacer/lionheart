import Foundation
import Testing

@testable import Lionheart

/// The gate that decides whether a signed-in athlete sees the app at all.
///
/// Its rules are not cosmetic. Get `applying(_:)` wrong in one direction and a new athlete is shown
/// five tabs that cannot work; wrong in the other and a blip on the network throws somebody out of
/// an app they were already using.
@Suite("Profile gate")
struct ProfileGateTests {
    @Test("A profile-less account is sent to onboarding")
    func noProfileMeansOnboarding() throws {
        let boot = try Decoding.decode(
            AthleteProfileBoot.self,
            #"{"name":"someone@example.com","hasCreatedProfile":false}"#)

        #expect(ProfileGate.undetermined.applying(.success(boot)) == .absent)
    }

    @Test("An account with a profile is let through, under its display name")
    func aProfileOpensTheTabs() throws {
        let boot = try Decoding.decode(
            AthleteProfileBoot.self,
            #"{"name":"Chase","hasCreatedProfile":true}"#)

        let gate = ProfileGate.undetermined.applying(.success(boot))

        #expect(gate == .present(displayName: "Chase"))
        #expect(gate.allowsTraining)
    }

    @Test("A boot read that fails before any answer is unreachable, not absent")
    func anUnreadableBootIsNotTreatedAsNoProfile() {
        let gate = ProfileGate.undetermined.applying(.failure(.offline("no route to host")))

        #expect(gate == .unreachable(.offline("no route to host")))
        // Showing the onboarding form here would offer to create a profile that may already exist.
        #expect(!gate.allowsTraining)
    }

    @Test("A failed refresh never throws an athlete out of the app they are already in")
    func aFailedRefreshKeepsASettledProfile() {
        let settled = ProfileGate.present(displayName: "Chase")

        let after = settled.applying(.failure(.offline("dropped")))

        #expect(after == settled)
        #expect(after.allowsTraining)
    }

    @Test("A failed refresh does not move an athlete off onboarding either")
    func aFailedRefreshKeepsASettledAbsence() {
        #expect(ProfileGate.absent.applying(.failure(.offline("dropped"))) == .absent)
    }

    @Test("Only a present profile opens the tabs")
    func onlyPresenceAllowsTraining() {
        #expect(!ProfileGate.undetermined.allowsTraining)
        #expect(!ProfileGate.absent.allowsTraining)
        #expect(!ProfileGate.unreachable(.notFound).allowsTraining)
        #expect(ProfileGate.present(displayName: "Chase").allowsTraining)
    }
}

/// What the onboarding form will and will not send.
///
/// The bounds are the client refusing to send a number that would read as nonsense on the context
/// card the model is handed — the server itself accepts an age of zero. So these tests are about
/// the form's own promise, not about guessing a server rule.
@Suite("Profile draft")
struct ProfileDraftStateTests {
    @Test("An untouched form is not submittable, and says why about all three fields")
    func anEmptyFormIsIncomplete() {
        let state = ProfileDraftState.of(displayName: "", age: "", weight: "")

        #expect(state.draft == nil)
        #expect(Set(state.problems) == [.nameMissing, .ageImplausible, .weightImplausible])
    }

    @Test("A complete form becomes a draft with the typed values")
    func aCompleteFormIsReady() {
        let state = ProfileDraftState.of(displayName: "  Chase ", age: "31", weight: "83.5")

        #expect(state == .ready(ProfileDraft(displayName: "Chase", age: 31, weight: 83.5)))
    }

    @Test("A name of nothing but whitespace is a missing name")
    func whitespaceIsNotAName() {
        let state = ProfileDraftState.of(displayName: "   ", age: "31", weight: "83.5")

        #expect(state.problems == [.nameMissing])
    }

    @Test("A weight typed with a decimal comma is read, not refused")
    func aCommaDecimalIsRead() {
        // A German or French keyboard's decimal separator. Refusing it would make the form
        // unusable for a value the athlete typed correctly.
        let state = ProfileDraftState.of(displayName: "Chase", age: "31", weight: "83,5")

        #expect(state.draft?.weight == 83.5)
    }

    @Test("An age of zero is refused, because the card would print it")
    func zeroIsNotAnAge() {
        #expect(ProfileDraftState.of(displayName: "Chase", age: "0", weight: "83").problems
            == [.ageImplausible])
    }

    @Test("An age that is not a number at all is refused rather than read as zero")
    func wordsAreNotAnAge() {
        #expect(ProfileDraftState.of(displayName: "Chase", age: "thirty", weight: "83").problems
            == [.ageImplausible])
    }

    @Test("A weight outside any plausible unit is refused")
    func anImplausibleWeightIsRefused() {
        #expect(ProfileDraftState.of(displayName: "Chase", age: "31", weight: "0").problems
            == [.weightImplausible])
        #expect(ProfileDraftState.of(displayName: "Chase", age: "31", weight: "9000").problems
            == [.weightImplausible])
    }

    @Test("Pounds and kilograms both fall inside the accepted range")
    func bothUnitsAreAccepted() {
        // The server's column has no unit, so the range has to admit a body weight in either.
        #expect(ProfileDraftState.of(displayName: "Chase", age: "31", weight: "83.5").draft != nil)
        #expect(ProfileDraftState.of(displayName: "Chase", age: "31", weight: "184").draft != nil)
    }

    @Test("Every field is reported, not just the first")
    func allProblemsAreAccumulated() {
        // A form that stopped at the first problem would make the athlete discover the other two
        // one submission at a time.
        let state = ProfileDraftState.of(displayName: "", age: "31", weight: "nonsense")

        #expect(Set(state.problems) == [.nameMissing, .weightImplausible])
    }
}
