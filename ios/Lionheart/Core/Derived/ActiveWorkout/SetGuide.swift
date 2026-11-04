import Foundation

/// What the entry row says beside the set being typed: last time at this position, the plates for
/// the weight, and the load moved where the weight is added to bodyweight.
///
/// One derivation for the three, because all three are read off the same two things — which set
/// this will be on the movement, and the weight in the field — and they change together as the
/// athlete types.
struct SetGuide: Equatable, Sendable {
    /// Last time's working set at the position this one will take, and the day it was.
    let lastTime: LastTime?
    /// The plates per side for the weight typed, on a barbell in a unit with a plate set.
    let plates: PlateLoading?
    /// Bodyweight plus the weight typed, on a movement whose weight is added to bodyweight.
    let systemLoad: SystemLoad?

    struct LastTime: Equatable, Sendable {
        let set: SetEntry
        let day: CalendarDay
        /// How the fields as typed stand against it.
        let versus: Versus
    }

    /// The fields against last time at this position, in the terms a lifter compares two sets in:
    /// the one figure that differs, or the estimated single where both do.
    enum Versus: Equatable, Sendable {
        /// The same load for the same reps.
        case same
        /// The same load; reps more (positive) or fewer.
        case reps(Int)
        /// The same reps; load more (positive) or less, in the set's unit.
        case load(Double)
        /// Both differ: the estimated single more or less, in the set's unit.
        case estimate(Double)
        /// Another unit, or fields that are not a set yet.
        case incomparable

        /// Whether the set as typed would beat last time's.
        var isAhead: Bool {
            switch self {
            case .reps(let reps): reps > 0
            case .load(let load): load > 0
            case .estimate(let change): change > 0
            case .same, .incomparable: false
            }
        }
    }

    var isEmpty: Bool { lastTime == nil && plates == nil && systemLoad == nil }

    static func of(
        _ draft: SetDraft,
        in workout: ActiveWorkout,
        previous: PreviousPerformance?,
        movement: MovementData?,
        bodyweight: BodyweightSeries
    ) -> SetGuide {
        guard draft.modality == .lift, let movementDataId = draft.movementDataId else {
            return SetGuide(lastTime: nil, plates: nil, systemLoad: nil)
        }

        // A warm-up has no position among the working sets, so last time says nothing about it.
        // Always said otherwise, with where the fields stand against it: last time is the target
        // a set to failure is aimed at, and "level", "+1 rep" or "+5 lb" is the answer the athlete
        // is reading the line for.
        let position = workout.workingSetCount(on: movementDataId)
        let prior = draft.kind.takesAPlace
            ? previous.flatMap { performance in
                performance.workingSet(at: position).map {
                    LastTime(set: $0, day: performance.date, versus: versus(draft, against: $0))
                }
            }
            : nil

        let weight = SetDraft.number(draft.weight)
        let plates: PlateLoading? = if let weight, movement?.isBarbell == true,
                                       let set = PlateSet.for(draft.weightUnit) {
            PlateLoading.of(weight, on: set)
        } else {
            nil
        }
        let systemLoad = weight.flatMap { weight in
            SystemLoad.of(
                weight: weight, unit: draft.weightUnit, setup: movement?.setup ?? .none,
                bodyweight: bodyweight.inForce(on: CalendarDay(workout.startedAt)))
        }

        return SetGuide(lastTime: prior, plates: plates, systemLoad: systemLoad)
    }

    /// Where the fields stand against a set from last time.
    static func versus(_ draft: SetDraft, against entry: SetEntry) -> Versus {
        guard case .lift(let before) = entry, draft.weightUnit == before.unit,
              let weight = SetDraft.number(draft.weight), let reps = SetDraft.whole(draft.reps), reps > 0
        else { return .incomparable }

        switch (weight == before.weight, reps == before.reps) {
        case (true, true): return .same
        case (true, false): return .reps(reps - before.reps)
        case (false, true): return .load(weight - before.weight)
        case (false, false):
            guard let now = EstimatedOneRepMax.of(weight: weight, reps: reps),
                  let then = EstimatedOneRepMax.of(weight: before.weight, reps: before.reps)
            else { return .incomparable }
            return .estimate(now - then)
        }
    }
}
