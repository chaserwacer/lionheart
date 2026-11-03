import Foundation

/// Where the active workout lives on disk.
///
/// Application Support rather than Caches or a temporary directory: the system may evict Caches
/// under pressure, and losing the file is the exact failure this exists to prevent. The directory
/// is excluded from backup — a half-finished workout restored onto a new phone weeks later would
/// be noise, and the server holds everything that matters once the session completes.
///
/// Writes are atomic. A crash partway through a write would otherwise leave truncated JSON, which
/// is worse than no file at all: the athlete would believe the workout was saved.
struct ActiveWorkoutDocument: Sendable {
    private let directoryURL: URL
    private let fileName: String

    init(directoryURL: URL? = nil, fileName: String = "active-workout.json") {
        self.directoryURL = directoryURL ?? Self.defaultDirectory()
        self.fileName = fileName
    }

    private var fileURL: URL { directoryURL.appending(path: fileName) }

    private static func defaultDirectory() -> URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL.temporaryDirectory
        return support.appending(path: "Lionheart", directoryHint: .isDirectory)
    }

    /// Reads the stored workout, or `nil` when there is none.
    ///
    /// A document this build cannot read, or one that no longer decodes, is treated as absent
    /// rather than thrown: there is no screen on which "your saved workout is corrupt" is a useful
    /// thing to say, and the app has to be able to start.
    func read() -> ActiveWorkout? {
        guard let data = try? Data(contentsOf: fileURL),
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
            try createDirectoryIfNeeded()
            let data = try JSONEncoder().encode(workout)
            try data.write(to: fileURL, options: [.atomic])
            return true
        } catch {
            return false
        }
    }

    func clear() {
        try? FileManager.default.removeItem(at: fileURL)
    }

    private func createDirectoryIfNeeded() throws {
        var directory = directoryURL
        guard !FileManager.default.fileExists(atPath: directory.path(percentEncoded: false)) else { return }

        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

        var excluded = URLResourceValues()
        excluded.isExcludedFromBackup = true
        try? directory.setResourceValues(excluded)
    }
}
