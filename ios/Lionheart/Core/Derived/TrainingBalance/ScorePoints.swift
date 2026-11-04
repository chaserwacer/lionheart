import Foundation

/// Points as the score's screens print them: one decimal, since a third of 35 is not a whole number.
enum ScorePoints {
    static func text(_ points: Double) -> String {
        points.formatted(.number.precision(.fractionLength(points.rounded() == points ? 0 : 1)))
    }

    /// "7.5/11.7", or "?/25" when unknown.
    static func earned(_ points: Double?, of outOf: Double) -> String {
        "\(points.map(text) ?? "?")/\(text(outOf))"
    }

    static func spoken(_ points: Double?, of outOf: Double) -> String {
        points.map { "Earned \(text($0)) of \(text(outOf)) points" } ?? "Unknown, out of \(text(outOf)) points"
    }
}
