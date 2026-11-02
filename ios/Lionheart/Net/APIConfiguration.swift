import Foundation

/// Where the app talks to.
///
/// The origin is a build setting (`LH_API_BASE_URL` in `ios/Configs/`) that reaches here through
/// the Info.plist, so pointing a device build at a LAN address or a tunnel is a configuration
/// change and not a code change. A missing or malformed value is a build that cannot work, so it
/// fails loudly at startup rather than producing requests to nowhere.
struct APIConfiguration: Equatable, Sendable {
    let baseURL: URL

    init(baseURL: URL) {
        self.baseURL = baseURL
    }

    static let infoPlistKey = "LHAPIBaseURL"

    init(bundle: Bundle = .main) {
        guard let raw = bundle.object(forInfoDictionaryKey: Self.infoPlistKey) as? String,
              let url = URL(string: raw.trimmingCharacters(in: .whitespaces)),
              url.scheme != nil, url.host != nil
        else {
            preconditionFailure(
                """
                \(Self.infoPlistKey) is missing or not a URL. It comes from LH_API_BASE_URL in \
                ios/Configs/; check that the xcconfig assembled it (a bare // starts a comment \
                there, which is why the URLs are built from LH_SLASH).
                """)
        }
        self.init(baseURL: url)
    }
}
