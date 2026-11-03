import Foundation
import Observation

/// Whether the signed-in athlete has a Lionheart profile, and the one write that gives them one.
///
/// It is read by the root rather than by a screen, because the answer decides which root there is:
/// the five tabs, or the one form that makes them work. Everything it decides lives in
/// `ProfileGate`; this type does the two calls and turns each outcome into a state.
@Observable
@MainActor
final class ProfileStore {
    private(set) var gate: ProfileGate = .undetermined
    private(set) var creation: ProfileCreationState = .editing

    private let users: UserAPI

    /// Kept so a retry during a slow boot read does not leave two answers racing to set the gate.
    private var load: Task<Void, Never>?

    init(users: UserAPI) {
        self.users = users
    }

    /// Asks the server whether this account has a profile. Safe to call again; the later answer
    /// wins by construction rather than by luck.
    func refresh() async {
        // The first answer of a launch comes from the device when it has one. The gate stands in
        // front of every tab, so a launch with no signal must not wait on the server to open the
        // app; the read below corrects the gate if the server says otherwise.
        if case .undetermined = gate, let recalled = users.recalledProfileBoot(.profileBoot) {
            gate = gate.applying(.success(recalled))
        }
        load?.cancel()
        let task = Task { await self.readBoot() }
        load = task
        await task.value
    }

    /// Empties everything scoped to one signed-in athlete. Registered with `AuthStore`, so the
    /// next athlete on this device cannot inherit the last one's gate — which here would mean
    /// walking straight into the tabs on an account that has no profile.
    func reset() {
        load?.cancel()
        load = nil
        gate = .undetermined
        creation = .editing
    }

    /// Creates the profile and opens the app.
    ///
    /// The gate is set from the answer rather than re-read, so the athlete does not wait for a
    /// second round trip to see the tabs they just earned. A 409 means a profile already exists —
    /// the other device won, or a retry after a reply that was lost in transit — so it is treated
    /// as the success it describes, and the boot read settles the display name.
    func createProfile(_ draft: ProfileDraft) async {
        guard !creation.isSubmitting else { return }

        creation = .submitting
        do {
            let profile = try await users.createProfile(draft)
            creation = .editing
            gate = .present(displayName: profile.displayName.nonEmpty ?? draft.displayName)
        } catch is CancellationError {
            creation = .editing
        } catch APIError.refused {
            creation = .editing
            await refresh()
        } catch {
            creation = .refused((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    /// Clears the refusal once the athlete edits what they typed, so the form stops showing it.
    func resumeEditing() {
        guard creation.failure != nil else { return }
        creation = .editing
    }

    /// Remembered, because this answer decides whether the tabs exist at all: a launch with no
    /// signal opens on what the server said last rather than on a retry screen, and the athlete
    /// can still log the workout they came to the gym for.
    private func readBoot() async {
        do {
            gate = gate.applying(.success(try await users.profileBoot(remembering: .profileBoot).value))
        } catch is CancellationError {
            return
        } catch {
            gate = gate.applying(
                .failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }
}
