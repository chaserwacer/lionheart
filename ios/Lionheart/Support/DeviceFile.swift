import Foundation

/// A file this app keeps for itself in Application Support, written atomically and kept out of
/// backups.
///
/// Application Support rather than Caches: the system may evict Caches under pressure, and every
/// file this app keeps is either work not yet on the server or the last answer a screen has to show
/// with no signal — losing either is the failure it exists to prevent. Excluded from backup because
/// all of it is about this device's moment: a half-finished workout or a month-old Today restored
/// onto a new phone is noise, and the server holds everything that lasts.
///
/// Writes are atomic. A crash partway through would otherwise leave a truncated file, which is
/// worse than none: the athlete would believe something was saved.
struct DeviceFile: Sendable {
    let directoryURL: URL
    let fileName: String

    init(directoryURL: URL? = nil, fileName: String) {
        self.directoryURL = directoryURL ?? Self.applicationDirectory
        self.fileName = fileName
    }

    /// `Application Support/Lionheart`, the one directory everything this app keeps lives under.
    static var applicationDirectory: URL {
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? URL.temporaryDirectory
        return support.appending(path: "Lionheart", directoryHint: .isDirectory)
    }

    var url: URL { directoryURL.appending(path: fileName) }

    /// The file's bytes, or `nil` when there is no file.
    func read() -> Data? {
        try? Data(contentsOf: url)
    }

    /// Replaces the file with `data`, creating the directory on first use.
    func write(_ data: Data) throws {
        try Self.createDirectory(at: directoryURL)
        try data.write(to: url, options: [.atomic])
    }

    func clear() {
        try? FileManager.default.removeItem(at: url)
    }

    /// Creates `directory` if it is not there, excluded from backup.
    static func createDirectory(at directory: URL) throws {
        guard !FileManager.default.fileExists(atPath: directory.path(percentEncoded: false)) else { return }

        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)

        var excluded = URLResourceValues()
        excluded.isExcludedFromBackup = true
        var marked = directory
        try? marked.setResourceValues(excluded)
    }
}
