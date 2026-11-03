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

/// How one performed set reads, whatever its shape.
///
/// Four set shapes carry different numbers, and a screen that wanted to print them all would have
/// to switch in the view. This names the two strings every shape can produce — the measurement and
/// the effort beside it — so the view prints two labels and the shapes stay closed here.
struct SetReading: Equatable, Sendable {
    /// "5 × 92.5 kg", "5 km in 24:10", or the words a freeform set kept.
    let measurement: String
    /// The RPE, where the set was rated.
    let rpe: Double?

    static func of(_ entry: SetEntry) -> SetReading {
        switch entry {
        case .lift(let set):
            return SetReading(
                measurement: "\(set.reps) × \(MeasurementText.plain(set.weight)) \(set.unit.abbreviation)",
                rpe: set.rpe.value)
        case .distanceTime(let set):
            return SetReading(
                measurement: "\(MeasurementText.plain(set.distance)) \(set.unit.abbreviation) in \(MeasurementText.clock(seconds: set.duration))",
                rpe: set.rpe.value)
        case .trailRun(let set):
            return SetReading(
                measurement: "\(MeasurementText.plain(set.distance)) \(set.unit.abbreviation) in \(MeasurementText.clock(seconds: set.duration)), \(MeasurementText.plain(set.elevationGainMeters)) m gain",
                rpe: set.rpe.value)
        case .freeform(let common):
            return SetReading(
                measurement: common.source.rawText?.nonEmpty ?? "Logged in words", rpe: nil)
        case .unrecognised(let type, let common):
            // The set shape is unknown to this build, but the words it was logged with — if it was
            // texted — are still the athlete's own record of it.
            return SetReading(
                measurement: common.source.rawText?.nonEmpty
                    ?? "A set shape this version cannot read (\(type))",
                rpe: nil)
        }
    }
}
