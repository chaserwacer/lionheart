import Foundation

/// What could be read off a photographed sheet: each figure found, and nothing for one that was not.
///
/// A figure missing from `values` was not legible; the review form shows it empty rather than
/// offering a likely number, because a plausible figure the athlete did not check is worse than an
/// empty box he has to fill from the paper.
struct SheetReading: Equatable, Sendable {
    var device: String?
    var unit: WeightUnit?
    var day: CalendarDay?
    var time: ClockTime?
    var values: [SheetField: Double] = [:]
    /// Figures the reading passes did not all read the same, with what they saw, most-read first —
    /// so the form can mark the box and say "read as 22.71 or 22.21", whether or not one reading
    /// carried the vote.
    var disagreements: [SheetField: [Double]] = [:]

    static let nothing = SheetReading()

    /// Several readings of one sheet — the whole page and tiles of it — as one.
    ///
    /// A figure is taken when more passes read one value than any other, and left unread on a tie.
    /// Any split is kept as a disagreement either way: a figure the passes read differently was
    /// hard to read, and the athlete is told so rather than handed the majority as if it were clean.
    /// A crease that misreads a digit the same way in every pass is beyond this; that is what the
    /// review form's arithmetic is for.
    static func merging(_ passes: [SheetReading]) -> SheetReading {
        let fields = Set(passes.flatMap(\.values.keys))
        let votes = fields.map { field in (field, Vote.of(passes.compactMap { $0.values[field] }, same: { abs($0 - $1) < 0.0005 })) }

        return SheetReading(
            device: Vote.of(passes.compactMap(\.device), same: ==).winner,
            unit: Vote.of(passes.compactMap(\.unit), same: ==).winner,
            day: Vote.of(passes.compactMap(\.day), same: ==).winner,
            time: Vote.of(passes.compactMap(\.time), same: ==).winner,
            values: Dictionary(uniqueKeysWithValues: votes.compactMap { field, vote in vote.winner.map { (field, $0) } }),
            disagreements: Dictionary(uniqueKeysWithValues: votes.compactMap { field, vote in
                vote.candidates.count > 1 ? (field, vote.candidates) : nil
            }))
    }

    /// The values the passes read for one figure, counted.
    private struct Vote<Value> {
        let winner: Value?
        let candidates: [Value]

        static func of(_ readings: [Value], same: (Value, Value) -> Bool) -> Vote {
            let tallies = readings.reduce(into: [(value: Value, count: Int)]()) { tallies, reading in
                if let index = tallies.firstIndex(where: { same($0.value, reading) }) {
                    tallies[index].count += 1
                } else {
                    tallies.append((reading, 1))
                }
            }
            let ranked = tallies.sorted { $0.count > $1.count }
            guard let top = ranked.first else { return Vote(winner: nil, candidates: []) }
            let isTied = ranked.count > 1 && ranked[1].count == top.count
            return Vote(winner: isTied ? nil : top.value, candidates: ranked.map(\.value))
        }
    }
}
