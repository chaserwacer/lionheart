import Foundation

/// Where finished workouts wait: one file each, so one that will not read cannot take the others
/// with it, and setting one down or taking one away never rewrites the rest.
struct FinishedWorkoutsDocument: Sendable {
    private let directoryURL: URL

    init(directoryURL: URL? = nil) {
        self.directoryURL = directoryURL
            ?? DeviceFile.applicationDirectory.appending(path: "finished-workouts", directoryHint: .isDirectory)
    }

    /// Every finished workout that reads, oldest finish first, which is the order they are
    /// delivered in. One that does not read is left on disk rather than deleted: it may be a newer
    /// build's, and it is somebody's workout.
    func read() -> [FinishedWorkout] {
        let files = (try? FileManager.default.contentsOfDirectory(
            at: directoryURL, includingPropertiesForKeys: nil)) ?? []
        return files
            .filter { $0.pathExtension == "json" }
            .compactMap { file in
                guard let data = try? Data(contentsOf: file),
                      let finished = try? JSONDecoder().decode(FinishedWorkout.self, from: data),
                      finished.workout.isReadable
                else { return nil }
                return finished
            }
            .sorted { $0.endedAt < $1.endedAt }
    }

    /// - Returns: whether it landed. A finish that is not on disk is not set down, and the caller
    ///   keeps the workout where it was.
    @discardableResult
    func write(_ finished: FinishedWorkout) -> Bool {
        do {
            try file(for: finished.id).write(try JSONEncoder().encode(finished))
            return true
        } catch {
            return false
        }
    }

    func remove(_ id: UUID) {
        file(for: id).clear()
    }

    func clear() {
        try? FileManager.default.removeItem(at: directoryURL)
    }

    private func file(for id: UUID) -> DeviceFile {
        DeviceFile(directoryURL: directoryURL, fileName: "\(id.uuidString).json")
    }
}
