import Foundation

/// Where the active workout lives on disk: one `DeviceFile`, rewritten whole on every change.
struct ActiveWorkoutDocument: Sendable {
    private let file: DeviceFile

    init(directoryURL: URL? = nil, fileName: String = "active-workout.json") {
        file = DeviceFile(directoryURL: directoryURL, fileName: fileName)
    }

    /// Reads the stored workout, or `nil` when there is none.
    ///
    /// A document this build cannot read, or one that no longer decodes, is treated as absent
    /// rather than thrown: there is no screen on which "your saved workout is corrupt" is a useful
    /// thing to say, and the app has to be able to start.
    func read() -> ActiveWorkout? {
        guard let data = file.read(),
              let workout = try? JSONDecoder().decode(ActiveWorkout.self, from: data),
              workout.isReadable
        else { return nil }
        return workout
    }

    /// Writes `workout`, replacing whatever was there.
    ///
    /// - Returns: whether it landed. The store surfaces a failure as an unsynced bar rather than
    ///   swallowing it, because an athlete who thinks their sets are safe when they are not is the
    ///   worst outcome available.
    @discardableResult
    func write(_ workout: ActiveWorkout) -> Bool {
        do {
            try file.write(try JSONEncoder().encode(workout))
            return true
        } catch {
            return false
        }
    }

    func clear() {
        file.clear()
    }
}
