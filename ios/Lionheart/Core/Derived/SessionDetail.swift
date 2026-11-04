import Foundation

/// A session's sets, grouped into the movements they were performed on.
///
/// The server deleted the movement wrapper entity deliberately — sets are a flat ordered list — so
/// the grouping is the client's job. It groups **consecutive** sets rather than all sets sharing a
/// movement, because returning to the bench press after squatting is two blocks in the session as
/// it happened, and collapsing them would rewrite the athlete's own order.
struct SessionDetail: Equatable, Sendable {
    let blocks: [MovementBlock]
    /// Sets whose shape carries no movement identity at all — a trail run, a freeform log. Their
    /// own section, because they have no movement name to sit under.
    let unattached: [IndexedSet]

    var isEmpty: Bool { blocks.isEmpty && unattached.isEmpty }

    /// One movement, and the consecutive run of sets performed on it.
    struct MovementBlock: Equatable, Sendable, Identifiable {
        /// Position of the block in the session, which is what gives it a stable identity when the
        /// same movement appears twice.
        let order: Int
        let movementDataId: UUID
        let movementName: String
        let sets: [IndexedSet]

        var id: Int { order }
    }

    /// A set with its position in the session, so "set 3" means the third set logged and keeps
    /// meaning that when the list is regrouped.
    struct IndexedSet: Equatable, Sendable, Identifiable {
        let order: Int
        let entry: SetEntry

        var id: Int { order }
    }
}

extension SessionDetail {
    /// What the sets that name no movement are called together: "Trail run" or "Ride" where they
    /// are all one such activity — a workout imported from Health is one set — and "Other work"
    /// for anything else.
    static func unattachedTitle(_ entries: [SetEntry]) -> String {
        let titles = Set(entries.map { entry -> String? in
            switch entry {
            case .trailRun: "Trail run"
            case .endurance(let set): set.activity.title
            case .lift, .distanceTime, .freeform, .unrecognised: nil
            }
        })
        guard titles.count == 1, let only = titles.first, let title = only else { return "Other work" }
        return title
    }

    static func of(_ sets: [SetEntry], catalogue: MovementCatalogue) -> SessionDetail {
        let indexed = sets.enumerated().map { IndexedSet(order: $0.offset, entry: $0.element) }

        var blocks: [MovementBlock] = []
        var unattached: [IndexedSet] = []

        for set in indexed {
            guard let movementDataId = set.entry.movementDataId else {
                unattached.append(set)
                continue
            }
            if let last = blocks.last, last.movementDataId == movementDataId {
                blocks[blocks.count - 1] = MovementBlock(
                    order: last.order,
                    movementDataId: last.movementDataId,
                    movementName: last.movementName,
                    sets: last.sets + [set])
            } else {
                blocks.append(MovementBlock(
                    order: blocks.count,
                    movementDataId: movementDataId,
                    movementName: catalogue.name(for: movementDataId),
                    sets: [set]))
            }
        }

        return SessionDetail(blocks: blocks, unattached: unattached)
    }
}
