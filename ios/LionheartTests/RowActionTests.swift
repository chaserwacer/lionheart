import Foundation
import Testing

@testable import Lionheart

/// What a row may offer, which is a question about the state of the thing in it.
///
/// The server refuses `/start` and `/skip` on anything that is not planned, so these claims are the
/// client's half of a rule the backend already enforces: a menu that offers a transition the state
/// cannot take is a menu whose only outcome is an error dialog.
@Suite("Session transitions")
struct SessionTransitionTests {
    @Test("A planned session can be started, skipped or deleted")
    func plannedOffersEverything() {
        #expect(SessionTransition.offered(on: .planned) == [.start, .skip, .delete])
    }

    @Test("A session planned for a day gone by can still be started, because training it late is normal")
    func missedIsStillStartable() {
        #expect(SessionTransition.offered(on: .missed).contains(.start))
    }

    @Test("A session that has happened can only be deleted — it cannot be started or skipped again")
    func completedOffersDeleteAlone() {
        #expect(SessionTransition.offered(on: .completed) == [.delete])
    }

    @Test("A skipped session can only be deleted, which is the only way back from a mistaken skip")
    func skippedOffersDeleteAlone() {
        #expect(SessionTransition.offered(on: .skipped) == [.delete])
    }

    @Test("A session in progress offers nothing: this device may be holding it as the active workout")
    func inProgressOffersNothing() {
        #expect(SessionTransition.offered(on: .inProgress).isEmpty)
    }

    @Test("A lifecycle state this build cannot read offers nothing")
    func unrecognisedOffersNothing() {
        #expect(SessionTransition.offered(on: .unrecognised("retired")).isEmpty)
    }

    @Test("Every destructive transition asks first, and the one that is not does not")
    func destructiveTransitionsConfirm() {
        for transition in SessionTransition.allCases {
            #expect((transition.confirmation != nil) == transition.isDestructive)
        }
    }
}

/// Resolving and reopening are one route and two directions, so a row must offer exactly one of
/// them — the one the injury is not already in.
@Suite("Injury actions")
struct InjuryActionTests {
    @Test("An injury being tracked can be resolved, not reopened")
    func activeOffersResolve() {
        #expect(InjuryAction.offered(on: anInjury(isActive: true)) == [.resolve, .delete])
    }

    @Test("A resolved injury can be taken up again, not resolved twice")
    func resolvedOffersReopen() {
        #expect(InjuryAction.offered(on: anInjury(isActive: false)) == [.reopen, .delete])
    }

    @Test("Only the delete asks first; resolving is undone by one tap and needs no dialog")
    func onlyDeleteConfirms() {
        #expect(InjuryAction.delete.confirmation != nil)
        #expect(InjuryAction.resolve.confirmation == nil)
        #expect(InjuryAction.reopen.confirmation == nil)
    }

    private func anInjury(isActive: Bool) -> Injury {
        Injury(
            injuryId: UUID(),
            name: "Left achilles",
            notes: "",
            injuryDate: CalendarDay(year: 2026, month: 9, day: 14),
            isActive: isActive,
            families: [],
            events: [])
    }
}

/// Where a row's action stands, and the one classification that matters: a cancelled action is not
/// a refusal to show the athlete.
@Suite("Action state")
struct ActionStateTests {
    @Test("A refused transition keeps the server's own words, which is what the athlete is told")
    func aRefusalKeepsTheServersWords() {
        let state = ActionState.refusal(
            from: APIError.refused(detail: "This session is not planned, so it cannot be skipped."))

        #expect(state == .refused(.refused("This session is not planned, so it cannot be skipped.")))
        #expect(state.refusal?.message == "This session is not planned, so it cannot be skipped.")
    }

    @Test("Cancellation is not a refusal: the screen asking has already gone")
    func cancellationIsNotARefusal() {
        #expect(ActionState.refusal(from: CancellationError()) == .idle)
        #expect(ActionState.refusal(from: CancellationError()).refusal == nil)
    }

    @Test("A transport failure is stated as one rather than as a refusal of the transition")
    func transportFailureIsOffline() {
        #expect(ActionState.refusal(from: APIError.transport("no route to host"))
            == .refused(.offline("no route to host")))
    }

    @Test("A refusal or a not-found shows the screen was out of date; it is read again")
    func refusalsRevealAnOutdatedScreen() {
        #expect(ActionState.refused(.refused("A skipped session cannot be skipped.")).revealsOutdatedScreen)
        #expect(ActionState.refused(.notFound).revealsOutdatedScreen)
    }

    @Test("A failure that says nothing about the server's state does not call for a re-read")
    func otherFailuresDoNotRevealAnOutdatedScreen() {
        #expect(!ActionState.refused(.offline("no route to host")).revealsOutdatedScreen)
        #expect(!ActionState.refused(.unavailable("The server had a problem.")).revealsOutdatedScreen)
        #expect(!ActionState.idle.revealsOutdatedScreen)
        #expect(!ActionState.running.revealsOutdatedScreen)
    }

    @Test("Only a running action is running, so one tap cannot start two")
    func onlyRunningIsRunning() {
        #expect(ActionState.running.isRunning)
        #expect(!ActionState.idle.isRunning)
        #expect(!ActionState.refused(.notFound).isRunning)
    }
}
