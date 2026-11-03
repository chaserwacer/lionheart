import Foundation
import Observation

/// The in-flight workout, and the only thing that writes it to disk.
///
/// It is a store rather than a screen's state because the workout bar is visible from all five tabs
/// — there is no one screen that owns it. Every change persists before anything else happens, which
/// is the ordering that makes a force-quit survivable: if the app dies between the write and the
/// server push, the next launch finds the workout and the next push carries it.
///
/// **What this build does and does not do.** It knows whether a workout is active, when it started,
/// and whether everything local has reached the server, and it persists all three. The logging
/// behaviour — appending sets, the rest clock, the current movement, the coalesced
/// `PUT /{id}/sets` push and `POST /{id}/complete` — is the training-feature agent's. The seam is
/// here and correct about persistence so that work is additive.
@Observable
@MainActor
final class ActiveWorkoutStore {
    /// The workout in flight, or nothing. `nil` is right here rather than a case: there is no
    /// third state, and the bar renders nothing for the absent one.
    private(set) var workout: ActiveWorkout?

    /// Whether the workout on screen is also the workout on disk.
    ///
    /// A separate fact from `ActiveWorkout.isSynced`, which is about the *server*. Both can fail
    /// independently and they want different words: unsynced means the next push will carry it,
    /// unpersisted means a force-quit would lose it.
    private(set) var isPersisted = true

    private let document: ActiveWorkoutDocument
    private let now: @Sendable () -> Date

    init(document: ActiveWorkoutDocument = ActiveWorkoutDocument(), now: @escaping @Sendable () -> Date = { Date() }) {
        self.document = document
        self.now = now
    }

    /// Reads whatever survived the last run. Called once, from the app's `.task`.
    ///
    /// Reading is off the main actor because it touches the file system, and the app's first frame
    /// should not wait on a disk read to draw.
    func restore() async {
        workout = await Self.read(from: document)
        isPersisted = true
    }

    /// Begins a workout against a session the server has already started.
    ///
    /// It takes the session id rather than creating one, because the server is the thing that
    /// decides a session exists: a workout the device invented would have nothing to push to.
    func begin(sessionId: UUID, startedAt: Date? = nil) {
        adopt(ActiveWorkout(sessionId: sessionId, startedAt: startedAt ?? now()))
    }

    /// Ends the workout locally.
    ///
    /// Only call this once the server has accepted the completion: clearing the document is what
    /// makes the local copy unrecoverable, so a 2xx has to come first.
    func end() {
        workout = nil
        isPersisted = true
        document.clear()
    }

    func markSynced(_ synced: Bool) {
        guard let workout, workout.isSynced != synced else { return }
        adopt(workout.markingSynced(synced))
    }

    /// Writes the value, then publishes it.
    ///
    /// The order is the whole point and is easy to reverse by accident: publishing first would
    /// render a workout that is not yet on disk, so a crash in between would lose something the
    /// athlete had already seen confirmed.
    private func adopt(_ next: ActiveWorkout) {
        isPersisted = document.write(next)
        workout = next
    }

    /// Reading the file off the main actor. `@concurrent` states the offload rather than relying on
    /// the current default for a nonisolated `async` function.
    @concurrent
    private nonisolated static func read(from document: ActiveWorkoutDocument) async -> ActiveWorkout? {
        document.read()
    }
}
