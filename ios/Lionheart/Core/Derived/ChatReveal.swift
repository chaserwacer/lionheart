import Foundation

/// How much of a just-arrived answer is on screen, in the moment after it arrives.
///
/// The server answers a chat message in one piece, after the model has finished. Setting the
/// answer down word by word over about a second reads as the answer arriving, the way a streamed
/// one does, and draws the eye down the bubble from its first line. It is fast on purpose: the
/// wait is already over, so the reveal must not add to it. Long answers take no longer than short
/// ones beyond `longest`.
enum ChatReveal {
    /// The pace, in characters a second, of a short answer.
    static let charactersPerSecond: Double = 900

    /// No answer takes longer than this to set down, however long it is.
    static let longest: TimeInterval = 1.2

    static func duration(of text: String) -> TimeInterval {
        min(longest, Double(text.count) / charactersPerSecond)
    }

    /// How many of `text`'s characters show `elapsed` seconds after it arrived: whole words only,
    /// so a word never appears half-written, and all of it once the reveal is over.
    static func visibleCount(of text: String, after elapsed: TimeInterval) -> Int {
        let total = text.count
        let duration = duration(of: text)
        guard elapsed < duration, duration > 0 else { return total }

        let target = Int((Double(total) * max(0, elapsed) / duration).rounded(.down))
        guard target > 0 else { return 0 }
        let characters = Array(text)
        var end = min(target, total)
        while end < total, !characters[end].isWhitespace { end += 1 }
        return end
    }

    static func isFinished(_ text: String, after elapsed: TimeInterval) -> Bool {
        elapsed >= duration(of: text)
    }
}
