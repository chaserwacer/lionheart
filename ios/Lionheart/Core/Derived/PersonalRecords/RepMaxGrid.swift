import Foundation

/// A movement's best weight at each rep count from one to twelve, in one weight unit.
///
/// Every rep count has a cell, held or not: an empty five is information — the lifter has never
/// recorded a five in this unit — and a grid that closed the gap would put the six where the five
/// should be. The server sends the records newest first, so the rep order is made here.
struct RepMaxGrid: Equatable, Sendable {
    let cells: [Cell]

    struct Cell: Equatable, Sendable, Identifiable {
        let reps: Int
        let record: PersonalRecord?
        /// Whether this cell was set in the most recent session that set any of the grid — the
        /// "my five went up" a lifter looks for first.
        let isNewest: Bool

        var id: Int { reps }

        /// The weight held at this rep count, if any.
        var weight: Double? {
            guard case .repMax(let lift) = record?.achievement else { return nil }
            return lift.weight
        }
    }

    /// Whether any rep count holds a record. A movement whose records all predate the grid has
    /// none, and twelve empty cells would be noise rather than information.
    var isEmpty: Bool { cells.allSatisfy { $0.record == nil } }

    /// The grid of one unit's rep-max records. Rep counts above the server's ceiling are kept if
    /// they arrive, rather than dropped, so a record is never on the server and missing here.
    static func of(_ repMaxes: [PersonalRecord]) -> RepMaxGrid {
        let byReps = repMaxes.reduce(into: [Int: PersonalRecord]()) { held, record in
            guard case .repMax(let reps) = record.achievement.kind else { return }
            held[reps] = RecordReading.newer(held[reps], record)
        }
        let newest = byReps.values.map(\.achievedAt).max()
        let highest = max(RecordKind.highestGradedRepCount, byReps.keys.max() ?? 0)

        return RepMaxGrid(cells: (1...highest).map { reps in
            let record = byReps[reps]
            return Cell(
                reps: reps,
                record: record,
                isNewest: record != nil && record?.achievedAt == newest)
        })
    }
}
