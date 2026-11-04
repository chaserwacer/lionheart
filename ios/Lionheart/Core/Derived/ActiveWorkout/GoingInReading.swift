import Foundation

/// What the logging surface's header says about the going-in state, decided from the server's
/// state, what is next, and the workout in flight.
///
/// The server rates and recommends; this only decides what bears on *this* workout: which injuries
/// touch a movement on screen, which priority lift is due and whether it is already here, and
/// whether the header is still worth its full height.
struct GoingInReading: Equatable, Sendable {
    let rating: GoingInRating
    /// The five parts in the order the rating weighs them.
    let parts: [Part]
    /// Active injuries, those touching a movement in this workout first.
    let injuries: [Injury]
    let plannedToday: [[String]]
    let recentMuscles: [MuscleGroup]
    let upNext: UpNextLine?
    /// Whether the header shows in full. It does until the first set of work, after which the
    /// athlete knows how he came in and every line it takes is a set pushed down the screen.
    let isExpanded: Bool

    struct Part: Equatable, Sendable, Identifiable {
        let kind: Kind
        let score: PartScore

        var id: Kind { kind }

        enum Kind: CaseIterable, Sendable {
            case readiness, autonomic, sleep, load, wellness
        }
    }

    struct Injury: Equatable, Sendable, Identifiable {
        let note: GoingInState.InjuryNote
        /// The movements in this workout its check-ins name, in the workout's order.
        let touching: [UUID]

        var id: UUID { note.id }
    }

    /// The server's next session as one line: which comes first, and the due lift's aim.
    struct UpNextLine: Equatable, Sendable {
        let first: NextSessionKind
        let families: [PatternFamily]
        /// The due families' priority lifts, each marked when it is already in this workout.
        let priorities: [Priority]
        let cardio: NextCardio
    }

    struct Priority: Equatable, Sendable, Identifiable {
        let priority: NextPriority
        let isInWorkout: Bool

        var id: UUID { priority.movementDataId }
    }

    /// - Parameter movements: the workout's movements in section order.
    static func of(
        _ state: GoingInState, next: NextSessions?, movements: [UUID], hasWork: Bool
    ) -> GoingInReading {
        let injuries = state.injuries
            .map { note in Injury(note: note, touching: movements.filter(note.movementDataIds.contains)) }
            .enumerated()
            .sorted { lhs, rhs in
                lhs.element.touching.isEmpty == rhs.element.touching.isEmpty
                    ? lhs.offset < rhs.offset
                    : !lhs.element.touching.isEmpty
            }
            .map(\.element)

        return GoingInReading(
            rating: state.rating,
            parts: [
                Part(kind: .readiness, score: state.readiness.score),
                Part(kind: .autonomic, score: state.autonomic.score),
                Part(kind: .sleep, score: state.sleep.score),
                Part(kind: .load, score: state.load.score),
                Part(kind: .wellness, score: state.wellness.score),
            ],
            injuries: injuries,
            plannedToday: state.plannedToday.filter { !$0.isEmpty },
            recentMuscles: state.recentMuscles.map(\.muscle),
            upNext: next.flatMap { upNext($0, movements: movements) },
            isExpanded: !hasWork)
    }

    private static func upNext(_ next: NextSessions, movements: [UUID]) -> UpNextLine? {
        switch next.lift {
        case .train(let families, _, let priorities, _):
            UpNextLine(
                first: next.first.session,
                families: families,
                priorities: priorities.map { Priority(priority: $0, isInWorkout: movements.contains($0.movementDataId)) },
                cardio: next.cardio)
        case .nothingLogged, .unrecognised:
            nil
        }
    }

    /// The scored part furthest under his usual day, when one is: the reason a rating is low is
    /// almost always one part, and naming it is the sentence worth having under the number.
    var weakest: Part? {
        parts
            .filter { ($0.score.score ?? Int.max) < Self.usualDay }
            .min { ($0.score.score ?? 0) < ($1.score.score ?? 0) }
    }

    /// The parts that could not be scored, which the rating was made without.
    var unread: [Part.Kind] {
        parts.filter { $0.score.score == nil }.map(\.kind)
    }

    /// Where a part sits on his usual day, which the server scores every part around.
    static let usualDay = 75

    /// The active injuries that name this movement, for a mark on its section.
    func injuries(touching movementDataId: UUID) -> [GoingInState.InjuryNote] {
        injuries.filter { $0.touching.contains(movementDataId) }.map(\.note)
    }
}
