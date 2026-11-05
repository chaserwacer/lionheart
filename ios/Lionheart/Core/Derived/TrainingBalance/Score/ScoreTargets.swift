import Foundation

/// Which of the week's targets the score shows met, so the screen can mark the moment one is hit.
///
/// A target is a measurement the athlete can still move this week — a family's hard sets, zone 2 or
/// quality minutes, the window's sessions — at or past the target the server scored it against. The
/// three blocks before the window are past weeks and nothing done now reaches them, so they never
/// count. Read from `ScoreBreakdown`'s cells, so "met" is the same figure the rows show.
enum ScoreTargets {
    /// The cells, by their breakdown id, that have reached their target.
    static func met(in score: TrainingScore) -> Set<String> {
        let movable = ScoreBreakdown.lifting(score.lifting, from: 0)
            + ScoreBreakdown.cardio(score.cardio, from: 0)
            + ScoreBreakdown.consistency(score.consistency, from: 0).prefix(1)
        return Set(movable.compactMap { cell in
            guard case .quantity(let measured, let target, _) = cell.reading, target > 0, measured >= target
            else { return nil }
            return cell.id
        })
    }

    /// The targets met in `current` that were not in `previous`: the ones just hit.
    static func newlyMet(from previous: TrainingScore, to current: TrainingScore) -> Set<String> {
        met(in: current).subtracting(met(in: previous))
    }
}
