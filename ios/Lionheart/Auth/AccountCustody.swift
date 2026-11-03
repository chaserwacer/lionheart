import CryptoKit
import Foundation

/// Which account the work held on this device belongs to.
///
/// A finished workout waiting for the server, and a workout started with no connection, exist
/// nowhere else. Signing out deliberately warns and then deletes them. A sign-out the server
/// forces, by refusing the refresh token, comes with no warning, and deleting them then would lose
/// training the athlete never chose to give up. So that path keeps them, and this records whose
/// they are: the next sign-in keeps them for the same account and empties them for any other.
///
/// The account is held as a digest of the sign-in email rather than the email: it is only ever
/// compared, never shown, and an address in a plain file is one more thing that could leak.
struct AccountCustody: Sendable {
    private let file: DeviceFile

    init(directoryURL: URL? = nil) {
        file = DeviceFile(directoryURL: directoryURL, fileName: "account-owner")
    }

    /// Whether the work on this device was held for `email`. No record means no: an install
    /// that has not signed in since this was introduced cannot say whose its work is.
    func isHeld(for email: String) -> Bool {
        file.read().flatMap { String(data: $0, encoding: .utf8) } == Self.fingerprint(of: email)
    }

    func hold(for email: String) {
        try? file.write(Data(Self.fingerprint(of: email).utf8))
    }

    func release() {
        file.clear()
    }

    /// The same address however it was typed: Identity matches emails without regard to case.
    static func fingerprint(of email: String) -> String {
        let normalised = email.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return SHA256.hash(data: Data(normalised.utf8)).map { String(format: "%02x", $0) }.joined()
    }
}
