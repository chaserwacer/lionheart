import Foundation

extension SetPrescription {
    /// Whether a logged set did at least what this target asked, in the target's own unit.
    ///
    /// A set row states its target only when this is false, so a set that hit its prescription
    /// reads as one line and a set that fell short shows what it was measured against. "At least"
    /// rather than "exactly": a heavier or longer set than asked has met the target, not missed it.
    /// RPE is not compared — the set's own rating is on the row whatever the target said.
    ///
    /// A set in another unit or modality is never a match: weights never convert, and a set whose
    /// shape differs from its target is exactly the case worth showing the target for.
    func isMet(by set: SetCommand) -> Bool {
        switch (self, set) {
        case (.lift(let target), .lift(let lift)):
            lift.unit == target.unit
                && lift.reps >= target.targetReps
                && lift.weight >= target.targetWeight
        case (.distanceTime(let target), .distanceTime(let run)):
            run.unit == target.unit
                && run.distance >= target.targetDistance
                && run.duration <= target.targetDuration
        case (.lift, .distanceTime), (.lift, .trailRun), (.lift, .freeform),
             (.distanceTime, .lift), (.distanceTime, .trailRun), (.distanceTime, .freeform),
             (.freeform, _), (.unrecognised, _):
            false
        }
    }
}
