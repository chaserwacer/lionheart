import Foundation

/// One thing the server says while it answers a message to the thread
/// (`docs/api-surface.md` → "The streamed exchange").
///
/// The last two cases end an exchange and nothing follows them. A stream that ends without either
/// has lost its connection, not its answer: the server finishes and stores it regardless.
enum ThreadReplyEvent: Equatable, Sendable {
    /// The next piece of the answer.
    case wrote(String)
    /// A round of tools is running. Whatever was written before it was a preamble and is withdrawn.
    case consulting(tools: [String])
    /// The exchange is stored: the session it went into, whole.
    case stored(ThreadSession)
    /// The exchange failed and the server kept nothing of it.
    case refused(APIError)
    /// An event this build does not know, from a newer server. Passed over.
    case unrecognised(name: String)
}
