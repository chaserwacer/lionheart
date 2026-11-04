import Foundation

extension String {
    /// The first letter upper case, the rest as written.
    var capitalisingFirst: String {
        guard let first else { return self }
        return first.uppercased() + dropFirst()
    }
}
