import Foundation

/// How the memory screen words where a memory came from.
enum MemoryWords {
    /// The server's limit on a memory's text, mirrored so the editor can refuse before sending.
    static let maxLength = 400

    /// "3 Nov 2026 · from chat", or "· corrected by you" for one he wrote himself.
    static func provenance(_ memory: ChatMemory) -> String {
        let source = switch memory.source {
        case .chat: "from chat"
        case .athlete: "corrected by you"
        case .unrecognised: "from chat"
        }
        guard let recorded = memory.recordedAt else { return source.prefix(1).uppercased() + source.dropFirst() }
        return "\(day(recorded)) · \(source)"
    }

    /// Who said a quoted message, and when.
    static func quoteLine(_ quote: MemoryQuote) -> String {
        let speaker = quote.byAthlete ? "You" : "Tempo"
        guard let at = quote.at else { return speaker }
        return "\(speaker), \(day(at)) \(at.formatted(date: .omitted, time: .shortened))"
    }

    /// When an earlier version held: from its recording to its replacement.
    static func versionLine(_ version: MemoryVersion) -> String {
        switch (version.recordedAt, version.replacedAt) {
        case let (recorded?, replaced?): "\(day(recorded)) to \(day(replaced))"
        case let (recorded?, nil): "From \(day(recorded))"
        case let (nil, replaced?): "Until \(day(replaced))"
        case (nil, nil): "Earlier"
        }
    }

    private static func day(_ instant: Date) -> String {
        instant.formatted(.dateTime.day().month(.abbreviated).year())
    }
}
