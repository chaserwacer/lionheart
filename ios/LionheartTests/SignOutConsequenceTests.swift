import Foundation
import Testing

@testable import Lionheart

/// What the sign-out confirmation tells the athlete their workout stands to lose.
///
/// Signing out deletes the workout document, so a confirmation that reassures the athlete about a
/// workout the server does not hold talks them into losing sets.
@Suite("Sign-out consequence")
struct SignOutConsequenceTests {
    @Test("No workout on the device: nothing is at stake")
    func noWorkout() {
        #expect(SignOutConsequence.of(nil) == .nothingInFlight)
    }

    @Test("A live session the server has every set of can be resumed from Train")
    func syncedLiveSessionIsResumable() {
        #expect(SignOutConsequence.of(AWorkout.live(sets: [AWorkout.lift()], sync: .synced))
            == .resumableFromTrain)
    }

    @Test("A live session with sets the server does not have loses them, whatever the reason",
          arguments: [WorkoutSyncState.pending, .failed("offline"), .refused("completed elsewhere")])
    func unsyncedLiveSessionLosesSets(sync: WorkoutSyncState) {
        #expect(SignOutConsequence.of(AWorkout.live(sets: [AWorkout.lift()], sync: sync))
            == .unsyncedSetsLost)
    }

    @Test("A workout written down afterwards is lost whole: nothing is sent until it is finished")
    func pastWorkoutIsLost() {
        #expect(SignOutConsequence.of(AWorkout.retro(sets: [AWorkout.lift()])) == .pastWorkoutLost)
    }
}

/// Whose the work kept through a forced sign-out is.
@Suite("Account custody")
struct AccountCustodyTests {
    private static func aCustody() -> AccountCustody {
        AccountCustody(directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
    }

    @Test("The same account signing back in finds its work, however the address was typed")
    func theSameAccountIsRecognised() {
        let custody = Self.aCustody()
        custody.hold(for: "chase@example.com")

        #expect(custody.isHeld(for: " Chase@Example.com "))
        #expect(!custody.isHeld(for: "someone@example.com"))
    }

    @Test("Nothing recorded, or released, is held for nobody")
    func releasedCustodyHoldsForNobody() {
        let custody = Self.aCustody()
        #expect(!custody.isHeld(for: "chase@example.com"))

        custody.hold(for: "chase@example.com")
        custody.release()

        #expect(!custody.isHeld(for: "chase@example.com"))
    }
}
