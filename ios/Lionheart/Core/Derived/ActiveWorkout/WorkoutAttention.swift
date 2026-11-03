import Foundation

/// Whether a workout in flight is in a state the athlete has to do something about.
///
/// Two failures that look alike and are not, which is why both are asked about here: the **disk**
/// (`ActiveWorkoutStore.isPersisted` — a force-quit would lose these sets) and the **server**
/// (`ActiveWorkout.sync` — the session does not have them yet). They fail independently and either
/// one costs the athlete work, so either one earns the interruption.
///
/// A rest clock running and a count of sets logged are not attention: they are what a workout in
/// progress looks like, and a bar that warns about them teaches the athlete to stop reading it.
///
/// It is a rule rather than a line of the bar because the bar does not always have room for two
/// lines — beside a minimized tab bar it has one — and this is what decides which line survives.
enum WorkoutAttention {
    /// Whether the athlete has to act on this workout.
    ///
    /// - Parameter savedToDisk: whether the store has the workout written down, which is a separate
    ///   question from whether the server has it.
    static func isNeeded(for workout: ActiveWorkout, savedToDisk: Bool) -> Bool {
        guard savedToDisk else { return true }
        switch workout.sync {
        case .refused, .failed: return true
        case .synced, .pending: return false
        }
    }
}
