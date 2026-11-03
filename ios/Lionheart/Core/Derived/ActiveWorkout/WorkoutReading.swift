import Foundation

/// The workout as the logging surface shows it.
///
/// One derivation rather than a view that re-answers each question, because the answers have to
/// agree: a set's number inside its movement, the target it was performed against, and which target
/// is next are the same counting done once. Doing it in the view would mean three loops that can
/// disagree about the same list.
///
/// A movement appears here as soon as it is **prescribed**, before anything is logged against it —
/// which is what makes opening a planned session show the plan rather than an empty screen.
struct WorkoutReading: Equatable, Sendable {
    /// Movements, in the order they first appear: prescribed ones in prescribed order, then any
    /// the athlete added, in the order they were first logged.
    let movements: [MovementSets]
    /// Sets that have no movement identity at all — a trail run, a set logged as words. The domain
    /// gives them no movement, so they are not forced into one.
    let unattached: [LoggedSet]
    let totals: WorkoutTotals

    /// Prescriptions that name no movement — a freeform target such as "mobility, 10 minutes".
    let looseTargets: [SetPrescription]

    /// The sets logged against one movement, and what was asked of it.
    struct MovementSets: Equatable, Sendable, Identifiable {
        let movementDataId: UUID
        /// Logged sets in order, numbered from one within this movement.
        let sets: [NumberedSet]
        /// Every target for this movement, in prescribed order.
        let targets: [SetPrescription]

        var id: UUID { movementDataId }

        /// The first target no set has been logged against.
        ///
        /// Targets are consumed in order *within the movement*: the nth set on a movement answers
        /// its nth target. Counting across the whole session instead would make a set on one
        /// movement tick off a target on another.
        var nextTarget: SetPrescription? {
            sets.count < targets.count ? targets[sets.count] : nil
        }

        /// Whether every target on this movement has a set against it. A movement with no targets
        /// is never "done" — there was nothing to finish.
        var isComplete: Bool { !targets.isEmpty && sets.count >= targets.count }

        var remainingTargetCount: Int { max(0, targets.count - sets.count) }
    }

    /// A logged set with its place in its movement.
    struct NumberedSet: Equatable, Sendable, Identifiable {
        /// 1-based, within the movement rather than the session: "set 3 of bench press".
        let number: Int
        /// Named `logged` and not `set`: inside a computed property's body `set` is the setter
        /// keyword, and `var id: UUID { set.id }` does not parse.
        let logged: LoggedSet
        /// The target this set answered, where the movement was prescribed that many sets deep.
        let target: SetPrescription?

        var id: UUID { logged.id }
    }

    static func of(_ workout: ActiveWorkout) -> WorkoutReading {
        of(sets: workout.sets, prescriptions: workout.prescriptions)
    }

    /// The same reading over the two lists it is made of.
    ///
    /// Taken separately so a *planned* session can be read before it is a workout: the screen that
    /// shows a plan before starting it wants the targets grouped exactly as the logging surface
    /// will group them, and inventing a workout to hold them would be a lie about what it has.
    static func of(sets: [LoggedSet], prescriptions: [SetPrescription]) -> WorkoutReading {
        // Grouped by hand rather than with `Dictionary(grouping:)`, which needs a non-optional key
        // and so would want a sentinel id for the sets that legitimately have no movement.
        let targetsByMovement: [UUID: [SetPrescription]] = prescriptions
            .reduce(into: [:]) { grouped, target in
                guard let movementDataId = target.movementDataId else { return }
                grouped[movementDataId, default: []].append(target)
            }
        let setsByMovement: [UUID: [LoggedSet]] = sets
            .reduce(into: [:]) { grouped, set in
                guard let movementDataId = set.movementDataId else { return }
                grouped[movementDataId, default: []].append(set)
            }

        let order = firstAppearances(
            of: prescriptions.compactMap(\.movementDataId) + sets.compactMap(\.movementDataId))

        return WorkoutReading(
            movements: order.map { movementDataId in
                let targets = targetsByMovement[movementDataId] ?? []
                let logged = setsByMovement[movementDataId] ?? []
                return MovementSets(
                    movementDataId: movementDataId,
                    sets: logged.enumerated().map { index, performed in
                        NumberedSet(
                            number: index + 1,
                            logged: performed,
                            target: index < targets.count ? targets[index] : nil)
                    },
                    targets: targets)
            },
            unattached: sets.filter { $0.movementDataId == nil },
            totals: WorkoutTotals.of(sets),
            looseTargets: prescriptions.filter { $0.movementDataId == nil })
    }

    /// The movement the athlete should be shown next.
    ///
    /// The one being worked on if it is still in the workout, else the first movement with a target
    /// left, else the last one a set was logged against. It is what the surface opens on, so that
    /// starting a planned session puts the first prescribed movement under the athlete's thumb.
    func focus(given current: UUID?) -> UUID? {
        if let current, movements.contains(where: { $0.movementDataId == current }) { return current }
        return movements.first(where: { !$0.isComplete })?.movementDataId
            ?? movements.last?.movementDataId
    }

    private static func firstAppearances(of ids: [UUID]) -> [UUID] {
        ids.reduce(into: []) { order, id in
            if !order.contains(id) { order.append(id) }
        }
    }
}
