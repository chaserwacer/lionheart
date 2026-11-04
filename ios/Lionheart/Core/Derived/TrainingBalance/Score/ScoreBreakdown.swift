import Foundation

/// The score as parts and their measurements, laid end to end on its 0–100 axis, so the screen
/// draws "push earned 11.7 of 11.7" from the server's arithmetic rather than a sub-score nobody
/// can check.
enum ScoreBreakdown {
    static func of(_ score: TrainingScore) -> [ScorePartBreakdown] {
        let parts: [(ScorePart, Double?, Double, (Double) -> [ScoreCell])] = [
            (.lifting, score.lifting.points, Double(score.lifting.outOf), { lifting(score.lifting, from: $0) }),
            (.cardio, score.cardio.points, Double(score.cardio.outOf), { cardio(score.cardio, from: $0) }),
            (.progress, score.progress.points, Double(score.progress.outOf), { progress(score.progress, from: $0) }),
            (.consistency, score.consistency.points, Double(score.consistency.outOf), { consistency(score.consistency, from: $0) }),
        ]
        return parts.reduce(into: [ScorePartBreakdown]()) { laid, part in
            let start = laid.last.map { $0.start + $0.outOf } ?? 0
            laid.append(ScorePartBreakdown(
                part: part.0, points: part.1, outOf: part.2, start: start, cells: part.3(start)))
        }
    }

    static func lifting(_ part: LiftingPart, from start: Double) -> [ScoreCell] {
        placed(from: start, part.families.map { scored in
            Measure(
                id: "lifting.\(scored.of.family.wire)",
                label: scored.of.family.name,
                reading: .quantity(measured: Double(scored.of.hardSets), target: scored.of.target.target, unit: .hardSets),
                points: scored.points, outOf: scored.outOf, weekly: scored.of.target)
        })
    }

    /// Zone 2 and quality when measured; one unknown cell across the part otherwise, so the part's
    /// width still reads and nothing reads as zero.
    static func cardio(_ part: CardioPart, from start: Double) -> [ScoreCell] {
        guard part.points != nil, !part.zones.isEmpty else {
            return placed(from: start, [Measure(
                id: "cardio.unknown", label: "Cardio", reading: .standing("Unknown"),
                points: nil, outOf: Double(part.outOf))])
        }
        return placed(from: start, part.zones.map { scored in
            Measure(
                id: "cardio.\(scored.of.band.name)",
                label: scored.of.band.name,
                reading: .quantity(measured: scored.of.minutes, target: scored.of.target.target, unit: .minutes),
                points: scored.points, outOf: scored.outOf, weekly: scored.of.target)
        })
    }

    /// Labelled by the family's lift, so "Push lift not trained" never reads against the hard sets
    /// that same family logged.
    static func progress(_ part: ProgressPart, from start: Double) -> [ScoreCell] {
        placed(from: start, part.families.map { scored in
            Measure(
                id: "progress.\(scored.of.family.wire)",
                label: "\(scored.of.family.name) lift",
                reading: .standing(standing(scored.of.lift)),
                points: scored.points, outOf: scored.outOf)
        })
    }

    /// The window first, then each block before it: "Last 7 days", "7–13 days ago", and so on.
    static func consistency(_ part: ConsistencyPart, from start: Double) -> [ScoreCell] {
        placed(from: start, part.blocks.enumerated().map { index, scored in
            Measure(
                id: "consistency.\(index)",
                label: blockLabel(index),
                reading: .quantity(measured: Double(scored.of.sessions), target: Double(part.target), unit: .sessions),
                points: scored.points, outOf: scored.outOf)
        })
    }

    static func blockLabel(_ index: Int) -> String {
        index == 0 ? "Last 7 days" : "\(index * 7)–\(index * 7 + 6) days ago"
    }

    /// What a family's priority earned on, in a word or two.
    static func standing(_ lift: PriorityLift) -> String {
        switch lift {
        case .none: "No lift"
        case .chosen(let chosen): LiftStanding.of(chosen.progress).verdict
        case .unrecognised: "Not readable"
        }
    }

    /// A cell before it has a place on the axis.
    private struct Measure {
        let id: String
        let label: String
        let reading: ScoreCell.Reading
        let points: Double?
        let outOf: Double
        var weekly: WeeklyTarget?
    }

    /// Measurements laid end to end from `start`, each as wide as the points it could earn.
    private static func placed(from start: Double, _ measures: [Measure]) -> [ScoreCell] {
        measures.reduce(into: [ScoreCell]()) { cells, measure in
            cells.append(ScoreCell(
                id: measure.id, label: measure.label, reading: measure.reading,
                points: measure.points, outOf: measure.outOf,
                start: cells.last.map { $0.start + $0.outOf } ?? start,
                weekly: measure.weekly))
        }
    }
}

/// A week's zone 2 and quality minutes against their targets, as rows state them outside the score.
extension ScoreBreakdown {
    static func zoneRows(_ part: CardioPart) -> [ScoreCell] {
        part.zones.map { scored in
            ScoreCell(
                id: "zone.\(scored.of.band.name)", label: scored.of.band.name,
                reading: .quantity(measured: scored.of.minutes, target: scored.of.target.target, unit: .minutes),
                points: nil, outOf: 0, start: 0, weekly: scored.of.target)
        }
    }
}
