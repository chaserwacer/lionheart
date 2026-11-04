import Foundation

/// What the Add sheet lists, in the order it lists it.
///
/// Mid-workout the wanted movement is nearly always a priority lift or one of the last few, so
/// those lead and need no typing; distance and time follow as their own section, because they are
/// the other kind of thing a session holds and were otherwise a separate button. With a query the
/// sheet is a search: every movement and every kind of cardio that answers it, and nothing else.
struct AddWorkMenu: Equatable, Sendable {
    /// The families' priority lifts, push, pull and legs order.
    let priorities: [MovementData]
    /// The recently used, less any already listed as a priority.
    let recent: [MovementData]
    /// Cardio with no movement, then the movements last logged as distance and time.
    let cardio: [CardioWork]
    let distanceMovements: [MovementData]
    /// The catalogue, or what in it answers the query.
    let movements: [MovementData]

    var isEmpty: Bool {
        priorities.isEmpty && recent.isEmpty && cardio.isEmpty && distanceMovements.isEmpty && movements.isEmpty
    }

    /// - Parameters:
    ///   - recentIds: the device's recently used list, most recent first.
    ///   - priorityIds: each family's priority lift, in family order.
    ///   - previous: last time on every movement; one last logged as distance and time is cardio.
    static func of(
        catalogue: [MovementData],
        recentIds: [UUID],
        priorityIds: [UUID],
        previous: [UUID: PreviousPerformance],
        query: String
    ) -> AddWorkMenu {
        let byId = Dictionary(catalogue.map { ($0.movementDataId, $0) }, uniquingKeysWith: { first, _ in first })
        let distance = catalogue.filter { movement in
            previous[movement.movementDataId]?.sets.contains { $0.command?.modality == .distanceTime } == true
        }

        guard query.trimmingCharacters(in: .whitespaces).isEmpty else {
            return AddWorkMenu(
                priorities: [],
                recent: [],
                cardio: CardioWork.offered.filter { $0.matches(query) },
                distanceMovements: [],
                movements: catalogue.filter { $0.matches(query) })
        }

        let priorities = unique(priorityIds).compactMap { byId[$0] }
        let listed = Set(priorities.map(\.movementDataId))
        return AddWorkMenu(
            priorities: priorities,
            recent: unique(recentIds).filter { !listed.contains($0) }.compactMap { byId[$0] },
            cardio: CardioWork.offered,
            distanceMovements: distance,
            movements: catalogue)
    }

    private static func unique(_ ids: [UUID]) -> [UUID] {
        ids.reduce(into: [UUID]()) { kept, id in
            if !kept.contains(id) { kept.append(id) }
        }
    }
}
