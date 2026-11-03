import Foundation

/// The workout as the logging surface shows it.
///
/// One derivation rather than a view that re-answers each question, because the answers have to
/// agree: a set's number inside its movement, the target it was performed against, and which target
/// is next are the same counting done once. Doing it in the view would mean three loops that can
/// disagree about the same list.
///
/// A movement appears here as soon as it is **prescribed or added**, before anything is logged
/// against it. Prescribed is what makes opening a planned session show the plan rather than an
/// empty screen; added is what gives a movement picked in an unplanned workout a place to log its
/// first set.
struct WorkoutReading: Equatable, Sendable {
    /// Movements, in the order they first appear: prescribed ones in prescribed order, then the
    /// ones the athlete added, in the order they added them, then any that only a logged set names
    /// — a session resumed from the server carries sets and no list of what was added.
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
        /// Logged sets in order, each numbered within its movement and its kind.
        let sets: [NumberedSet]
        /// Every target for this movement, in prescribed order.
        let targets: [SetPrescription]
        /// How many working sets the routine the workout started from gives this movement, or `nil`
        /// where no routine names it. Only consulted where nothing was prescribed: a plan's own
        /// targets are the more specific statement.
        let routineSets: Int?

        init(
            movementDataId: UUID, sets: [NumberedSet], targets: [SetPrescription], routineSets: Int? = nil
        ) {
            self.movementDataId = movementDataId
            self.sets = sets
            self.targets = targets
            self.routineSets = routineSets
        }

        var id: UUID { movementDataId }

        /// How many sets so far were the work, which is what targets and the plan count. A warm-up
        /// answers nothing.
        var workingSetCount: Int { sets.count(where: { $0.logged.command.isWork }) }

        /// How many working sets the movement is meant to get: one per target, else the routine's
        /// count, else none.
        var plannedSetCount: Int { targets.isEmpty ? (routineSets ?? 0) : targets.count }

        /// The first target no working set has been logged against.
        ///
        /// Targets are consumed in order *within the movement*: the nth working set on a movement
        /// answers its nth target. Counting across the whole session instead would make a set on
        /// one movement tick off a target on another.
        var nextTarget: SetPrescription? {
            workingSetCount < targets.count ? targets[workingSetCount] : nil
        }

        /// Whether every planned set on this movement has been logged. A movement with no plan is
        /// never "done" — there was nothing to finish.
        var isComplete: Bool { plannedSetCount > 0 && workingSetCount >= plannedSetCount }

        var remainingTargetCount: Int { max(0, targets.count - workingSetCount) }

        /// Where the movement stands against its plan, as the section header states it.
        var progress: TargetProgress {
            let planned = plannedSetCount
            let logged = workingSetCount
            if planned == 0 { return .unplanned }
            if logged <= planned { return .within(logged: logged, of: planned) }
            return .beyond(planned: planned, extra: logged - planned)
        }
    }

    /// A movement's sets against its targets.
    ///
    /// Three cases because logging past the plan is ordinary — a back-off set, one more because
    /// it moved well — and "7 of 3" is not a true sentence about it. Past the plan, every target
    /// has been answered and the rest are extra, which is what the header should say.
    enum TargetProgress: Equatable, Sendable {
        /// Nothing was prescribed, so there is no plan to measure against.
        case unplanned
        /// `logged` sets so far against `of` targets, from none up to exactly the plan.
        case within(logged: Int, of: Int)
        /// Every one of `planned` targets answered, and `extra` sets beyond them.
        case beyond(planned: Int, extra: Int)
    }

    /// A logged set with its place in its movement.
    struct NumberedSet: Equatable, Sendable, Identifiable {
        /// 1-based, within the movement rather than the session, and within the set's kind of work:
        /// working sets count 1, 2, 3 and warm-ups count apart, so a warm-up does not make the
        /// first working set "set 3".
        let number: Int
        /// Named `logged` and not `set`: inside a computed property's body `set` is the setter
        /// keyword, and `var id: UUID { set.id }` does not parse.
        let logged: LoggedSet
        /// The target this set answered, where the movement was prescribed that many sets deep.
        let target: SetPrescription?

        var id: UUID { logged.id }

        var isWarmUp: Bool { !logged.command.isWork }
    }

    static func of(_ workout: ActiveWorkout) -> WorkoutReading {
        of(
            sets: workout.sets,
            prescriptions: workout.prescriptions,
            added: workout.addedMovementDataIds,
            routine: workout.routine)
    }

    /// The same reading over the lists it is made of.
    ///
    /// Taken separately so a *planned* session can be read before it is a workout: the screen that
    /// shows a plan before starting it wants the targets grouped exactly as the logging surface
    /// will group them, and inventing a workout to hold them would be a lie about what it has.
    static func of(
        sets: [LoggedSet], prescriptions: [SetPrescription], added: [UUID] = [],
        routine: WorkoutRoutine? = nil
    ) -> WorkoutReading {
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
            of: prescriptions.compactMap(\.movementDataId) + added + sets.compactMap(\.movementDataId))

        return WorkoutReading(
            movements: order.map { movementDataId in
                let targets = targetsByMovement[movementDataId] ?? []
                let logged = setsByMovement[movementDataId] ?? []
                return MovementSets(
                    movementDataId: movementDataId,
                    sets: numbered(logged, against: targets),
                    targets: targets,
                    routineSets: routine?.workingSets(of: movementDataId))
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

    /// Numbers each set within its kind of work, and pairs each working set with the target it
    /// answers. A fold, because each set's number depends on how many of its kind came before.
    private static func numbered(_ logged: [LoggedSet], against targets: [SetPrescription]) -> [NumberedSet] {
        logged.reduce(into: (sets: [NumberedSet](), work: 0, warmUps: 0)) { state, performed in
            if performed.command.isWork {
                let index = state.work
                state.work += 1
                state.sets.append(NumberedSet(
                    number: state.work,
                    logged: performed,
                    target: index < targets.count ? targets[index] : nil))
            } else {
                state.warmUps += 1
                state.sets.append(NumberedSet(number: state.warmUps, logged: performed, target: nil))
            }
        }.sets
    }

    private static func firstAppearances(of ids: [UUID]) -> [UUID] {
        ids.reduce(into: []) { order, id in
            if !order.contains(id) { order.append(id) }
        }
    }
}
