import Foundation

/// Where the app leaves a `TodayGlance` for the widgets: one JSON file in the App Group container
/// the app and `LionheartWidgets` share.
///
/// Compiled into both targets. The app writes, the extension reads, and nothing else touches it.
/// No container — a build without the App Group entitlement — reads as nothing kept and writes
/// nothing, so the widgets show their empty state rather than the app failing.
struct TodayGlanceFile: Sendable {
    /// The App Group both targets declare in their entitlements (`ios/project.yml`).
    static let appGroup = "group.com.chasebalmer.lionheart"

    let url: URL?

    init(url: URL? = FileManager.default
        .containerURL(forSecurityApplicationGroupIdentifier: Self.appGroup)?
        .appending(path: "today-glance.json")) {
        self.url = url
    }

    func read() -> TodayGlance? {
        guard let url, let data = try? Data(contentsOf: url) else { return nil }
        return try? Self.decoder.decode(TodayGlance.self, from: data)
    }

    /// Writes `glance` whole, atomically, so the extension never reads half of one.
    func write(_ glance: TodayGlance) throws {
        guard let url else { return }
        try Self.encoder.encode(glance).write(to: url, options: [.atomic, .completeFileProtectionUntilFirstUserAuthentication])
    }

    /// Removes what is kept — what signing out does, so the Lock Screen never shows one athlete's
    /// readiness to the next.
    func remove() {
        guard let url else { return }
        try? FileManager.default.removeItem(at: url)
    }

    private static let encoder: JSONEncoder = {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        return encoder
    }()

    private static let decoder: JSONDecoder = {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }()
}
