import Foundation
import Security

/// Where the signed-in athlete's tokens live.
///
/// The Keychain and not `UserDefaults`: a refresh token is a long-lived credential, and a plist in
/// the app container is readable by anything that can read the container. `kSecAttrAccessible`
/// is `AfterFirstUnlock` so a background refresh works while the phone is locked, which
/// `WhenUnlocked` would prevent, and the item is not synchronised to iCloud — a bearer token is
/// this device's, and a second device signs in for itself.
struct KeychainTokenStore: Sendable {
    private let service: String
    private let account: String

    init(service: String = "com.chasebalmer.lionheart.tokens", account: String = "identity") {
        self.service = service
        self.account = account
    }

    private var query: [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
    }

    /// The stored tokens, or `nil` when there are none or what is there no longer decodes.
    ///
    /// A stored shape that stopped decoding is treated as absent on purpose: the athlete signs in
    /// again, which is a recoverable inconvenience, where throwing would leave the app unable to
    /// reach its signed-out state either.
    func load() -> TokenSet? {
        var request = query
        request[kSecReturnData as String] = true
        request[kSecMatchLimit as String] = kSecMatchLimitOne

        var item: CFTypeRef?
        guard SecItemCopyMatching(request as CFDictionary, &item) == errSecSuccess,
              let data = item as? Data
        else { return nil }

        return try? JSONDecoder().decode(TokenSet.self, from: data)
    }

    /// Writes `tokens`, replacing whatever was there.
    ///
    /// - Returns: whether the write landed. A caller that cannot store a credential has to know,
    ///   because the alternative is an athlete who appears signed in until the app restarts.
    @discardableResult
    func save(_ tokens: TokenSet) -> Bool {
        guard let data = try? JSONEncoder().encode(tokens) else { return false }

        let attributes: [String: Any] = [
            kSecValueData as String: data,
            kSecAttrAccessible as String: kSecAttrAccessibleAfterFirstUnlock,
        ]

        let updated = SecItemUpdate(query as CFDictionary, attributes as CFDictionary)
        if updated == errSecSuccess { return true }
        guard updated == errSecItemNotFound else { return false }

        return SecItemAdd(query.merging(attributes) { _, new in new } as CFDictionary, nil) == errSecSuccess
    }

    func clear() {
        SecItemDelete(query as CFDictionary)
    }
}
