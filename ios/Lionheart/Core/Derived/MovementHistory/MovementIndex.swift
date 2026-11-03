import Foundation

/// Every movement the log holds, with how much of it there is — the way into a movement's page
/// that does not need a session to start from.
///
/// Most recently performed first, because the movement wanted is usually one being trained now.
struct MovementIndex: Equatable, Sendable {
    let entries: [Entry]

    struct Entry: Equatable, Sendable, Identifiable {
        let movementDataId: UUID
        let name: String
        let sessionCount: Int
        let lastDay: CalendarDay

        var id: UUID { movementDataId }
    }

    func matching(_ query: String) -> [Entry] {
        guard let wanted = query.nonEmpty else { return entries }
        return entries.filter { $0.name.localizedStandardContains(wanted) }
    }

    static func of(
        _ sessions: [TrainingSession], catalogue: MovementCatalogue, today: CalendarDay
    ) -> MovementIndex {
        let tally = sessions
            .filter { SessionLog.kind(of: $0, today: today).isPerformed }
            .reduce(into: [UUID: (count: Int, last: CalendarDay)]()) { tally, session in
                for id in Set(session.sets.compactMap(\.movementDataId)) {
                    let held = tally[id]
                    tally[id] = ((held?.count ?? 0) + 1, max(held?.last ?? session.date, session.date))
                }
            }
        return MovementIndex(entries: tally
            .map { id, figures in
                Entry(
                    movementDataId: id, name: catalogue.name(for: id),
                    sessionCount: figures.count, lastDay: figures.last)
            }
            .sorted { ($0.lastDay, $1.name) > ($1.lastDay, $0.name) })
    }
}

extension TrainingSession {
    /// Every movement the session names, in its sets or its prescriptions — what a screen holding
    /// it needs the catalogue to know.
    var referencedMovementIds: Set<UUID> {
        let prescribed: [UUID] = switch self {
        case .planned(let planned): planned.prescriptions.compactMap(\.movementDataId)
        case .active, .completed, .skipped, .unrecognised: []
        }
        return Set(sets.compactMap(\.movementDataId) + prescribed)
    }
}
