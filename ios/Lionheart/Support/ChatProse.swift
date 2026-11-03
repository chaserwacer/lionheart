import Foundation

/// The model's answers, as something `Text` can draw.
///
/// The model writes Markdown — `**emphasis**`, `-` bullets, blank lines between paragraphs — and
/// `Text` interprets only *inline* Markdown. So this parses with
/// `.inlineOnlyPreservingWhitespace`, which is the combination that gets both halves right: bold
/// and italics become attributes, and the newlines that carry the paragraph and list structure
/// survive instead of being collapsed into one block of prose.
///
/// A list marker stays the literal `-` the model wrote. That is deliberate: rebuilding block
/// structure would mean writing a Markdown renderer, and a dash at the start of a line already
/// reads as a bullet.
enum ChatProse {
    /// `markdown` as attributed text, or the plain words when it will not parse.
    ///
    /// Falling back rather than failing is the point: the answer is the thing the athlete waited
    /// tens of seconds for, and a parser complaint is never a better screen than the raw words.
    static func attributed(_ markdown: String) -> AttributedString {
        let trimmed = markdown.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let parsed = try? AttributedString(
            markdown: trimmed,
            options: AttributedString.MarkdownParsingOptions(
                interpretedSyntax: .inlineOnlyPreservingWhitespace))
        else { return AttributedString(trimmed) }
        return parsed
    }
}
