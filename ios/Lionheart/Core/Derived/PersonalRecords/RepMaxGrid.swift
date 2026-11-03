import Foundation

/// A movement's best weight at each rep count from one to twelve, in one weight unit.
///
/// The first cell is the heaviest set. Every set contains a single, so the one-rep max is the
/// heaviest set by definition and the server keeps no second record of it. The other cells are the
/// rep maxes, each held by a set of at least that many reps, which is why the weights never rise
/// as the count does.
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
        var weight: Double? { record?.achievement.performedSet?.weight }

        /// How many reps the set holding this cell had, when it had more than the cell's count.
        ///
        /// A five-rep max from a clean set of five and one from the front of a set of eight are the
        /// same record and different stories, so the grid says which.
        var longerSetReps: Int? {
            guard let performed = record?.achievement.performedSet?.reps, performed > reps else {
                return nil
            }
            return performed
        }
    }

    /// Whether any rep count past the single holds a record.
    ///
    /// The single is the heaviest set, which a movement whose records predate the grid holds on
    /// its own; eleven empty cells beside it would be noise rather than information, so such a
    /// movement shows its heaviest set as a row instead.
    var holdsRepMaxes: Bool { cells.contains { $0.reps > 1 && $0.record != nil } }

    /// The grid of one unit's lifting records: its heaviest set in the first cell, its rep maxes
    /// after. Rep counts above the server's ceiling are kept if they arrive, rather than dropped,
    /// so a record is never on the server and missing here.
    static func of(heaviestSet: PersonalRecord?, repMaxes: [PersonalRecord]) -> RepMaxGrid {
        let byReps = repMaxes.reduce(into: [Int: PersonalRecord]()) { held, record in
            guard case .repMax(let reps) = record.achievement.kind else { return }
            held[reps] = RecordReading.newer(held[reps], record)
        }
        let held = byReps.merging(heaviestSet.map { [1: $0] } ?? [:]) { _, heaviest in heaviest }
        let newest = held.values.map(\.achievedAt).max()
        let highest = max(RecordKind.repMaxReps.upperBound, held.keys.max() ?? 0)

        return RepMaxGrid(cells: (1...highest).map { reps in
            let record = held[reps]
            return Cell(
                reps: reps,
                record: record,
                isNewest: record != nil && record?.achievedAt == newest)
        })
    }
}
