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
        let position = workout.sets.count(where: { $0.movementDataId == movementDataId && $0.command.isWork })
        // Said only when the fields differ from it: a row opened on last time's numbers already
        // shows them, and the line is for comparing once the athlete has changed one.
        let prior = draft.kind.isWork
            ? previous.flatMap { performance in
                performance.workingSet(at: position).map { LastTime(set: $0, day: performance.date) }
            }
            .flatMap { lastTime in isShowing(lastTime.set, in: draft) ? nil : lastTime }
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

    /// Whether the fields already hold last time's load and reps.
    private static func isShowing(_ entry: SetEntry, in draft: SetDraft) -> Bool {
        guard case .lift(let before) = entry else { return false }
        return SetDraft.number(draft.weight) == before.weight
            && SetDraft.whole(draft.reps) == before.reps
            && draft.weightUnit == before.unit
    }
}
