import Foundation

/// How far the answer to the athlete's question has got, while the server is still working on it.
enum ReplyProgress: Equatable, Sendable {
    /// Nothing back yet: the model is reading the question and thinking.
    case thinking
    /// The model is reading his data through these tools, and has written nothing since.
    case consulting(tools: [String])
    /// The answer so far, as it is being written.
    case writing(String)

    /// After `event`, for the events that move an answer along rather than end it.
    ///
    /// A round of tools withdraws what was written before it: that text was the model's preamble,
    /// and only the last round's text is the answer the server stores.
    func after(_ event: ThreadReplyEvent) -> ReplyProgress {
        switch (self, event) {
        case (.writing(let so), .wrote(let piece)): .writing(so + piece)
        case (.thinking, .wrote(let piece)), (.consulting, .wrote(let piece)): .writing(piece)
        case (_, .consulting(let tools)): .consulting(tools: tools)
        case (_, .stored), (_, .refused), (_, .unrecognised): self
        }
    }

    /// The answer as written so far, or `nil` while nothing of it has been.
    var written: String? {
        switch self {
        case .writing(let text): text
        case .thinking, .consulting: nil
        }
    }

    /// What the model is reading, in words: `GetTrainingScore` and `SearchMemory` read as
    /// "training score and memory".
    ///
    /// The tool names are the server's code names, so this is a reading of them, not a catalogue:
    /// a tool added on the server reads sensibly here without a release.
    static func reading(_ tools: [String]) -> String {
        tools.map(subject).formatted(.list(type: .and))
    }

    private static let verbs: Set<String> = ["Get", "Search", "List", "Find"]

    private static func subject(of tool: String) -> String {
        let words = tool.reduce(into: [String]()) { words, character in
            if character.isUppercase || words.isEmpty {
                words.append(String(character))
            } else {
                words[words.count - 1].append(character)
            }
        }
        let named = words.first.map(verbs.contains) == true ? Array(words.dropFirst()) : words
        // "Oura" is a name and keeps its capital; every other word is a common noun.
        return named.map { $0 == "Oura" ? $0 : $0.lowercased() }.joined(separator: " ")
    }
}
