import Foundation

/// One part of the score — lifting, cardio, progress, consistency — and its measurements.
struct ScorePartBreakdown: Equatable, Sendable, Identifiable {
    let part: ScorePart
    let points: Double?
    let outOf: Double
    let start: Double
    let cells: [ScoreCell]

    var id: ScorePart { part }

    var share: Double {
        guard let points, outOf > 0 else { return 0 }
        return min(1, max(0, points / outOf))
    }

    /// The part's measurements in one line, for the row that opens it: "Push 8/8 · Pull 6/8 · Legs 9/8".
    var summary: String {
        if points == nil { return "Unknown this week" }
        if part == .consistency { return blocksSummary }
        return cells.map { cell in
            switch cell.reading {
            case .quantity(let measured, let target, _):
                "\(cell.label) \(MeasurementText.plain(measured.rounded()))/\(MeasurementText.plain(target))"
            case .standing(let words):
                "\(cell.label) \(words.lowercased())"
            }
        }
        .joined(separator: " · ")
    }

    /// "3 · 2 · 2 · 0 of 3 a week, newest first": four blocks read as one run of counts.
    private var blocksSummary: String {
        let counts = cells.compactMap { cell -> (Double, Double)? in
            guard case .quantity(let measured, let target, _) = cell.reading else { return nil }
            return (measured, target)
        }
        guard let target = counts.first?.1 else { return "" }
        let run = counts.map { MeasurementText.plain($0.0) }.joined(separator: " · ")
        return "\(run) of \(MeasurementText.plain(target)) a week, newest first"
    }
}
