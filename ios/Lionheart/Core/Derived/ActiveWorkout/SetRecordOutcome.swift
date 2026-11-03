import Foundation

/// Whether a set just logged beat a record standing against its movement.
///
/// A lift competes for the records the server's detector stages for a set: the heaviest set, the
/// rep max at its own rep count and at every count below it — a set of seven contains a set of six,
/// because those six reps were performed — and the best set volume. Beating any of them is news the
/// athlete wants while the bar is still loaded; "that was a five-rep max" is the sentence the
/// rep-max grid exists for.
///
/// What is deliberately not said here:
///
/// - **Records the set would be the first of.** A movement's first rep max at seven, or its first
///   record in a unit the athlete just switched to, is a record the server will set — but saying
///   so would make one set of twelve on a new movement announce eleven rep maxes, and every set of
///   the first workout in kilograms announce something, so the badge would mean nothing by the time
///   it was earned. Only beating a standing record is news.
/// - **Best session volume, and the distance records.** Those are decided over a whole session or
///   a whole effort, which the server reads on completion; the device would have to re-derive its
///   distance conversions and paces to guess at them, and a guess that disagrees with the record
///   that lands is worse than silence.
///
/// The records come from the summaries as they stood when the workout began, so the sets already
/// logged on the movement this workout raise the bar too: once one set has beaten a record, a later
/// set has to beat that set to say so. Three sets of 60 × 10 against a 55 × 10 record are one new
/// rep max, not three. The server remains the authority on what the session establishes.
enum SetRecordOutcome: Equatable, Sendable {
    /// Not a lift, or a warm-up — which never sets a record — so nothing to say mid-workout.
    case notApplicable
    /// No record stands in this set's unit for any of the records it competes for.
    case noRecordYet
    case none
    /// The records it beat, the one most worth saying first: the heaviest set, then the rep maxes
    /// from the highest count down, then the set volume.
    case beats(RecordKind, also: [RecordKind])

    var isWorthSaying: Bool {
        switch self {
        case .notApplicable, .noRecordYet, .none: false
        case .beats: true
        }
    }

    /// - Note: Only records held in the set's own unit are compared. Weights never convert — 225 lb
    ///   is a larger number and a lighter lift than 110 kg — and the unit is part of a record's
    ///   identity, so a pounds set competes with the pounds records and a kilograms record is not
    ///   one it could beat.
    ///
    /// `earlier` is the workout's sets logged before this one, in any order and on any movement;
    /// only lifts of the same movement in the same unit raise a bar.
    static func of(
        _ command: SetCommand, after earlier: [SetCommand], against held: SubjectRecords?
    ) -> SetRecordOutcome {
        guard let lift = command.liftPart, lift.kind.isWork else { return .notApplicable }

        let standing = (held?.records ?? []).filter {
            $0.standing == .standing && $0.achievement.heldIn == lift.unit
        }
        let bestEarlier = bestFigures(in: earlier, competingWith: lift)
        let contests = contested(by: lift).compactMap { kind, figure in
            standingFigure(of: kind, in: standing).map { record in
                (kind, beats: figure > max(record, bestEarlier[kind] ?? record))
            }
        }

        guard !contests.isEmpty else { return .noRecordYet }
        let beaten = contests.filter(\.beats).map(\.0)
        guard let first = beaten.first else { return .none }
        return .beats(first, also: Array(beaten.dropFirst()))
    }

    /// The records a lift competes for, with its figure for each, in the order they are worth
    /// saying — mirroring the server's `RecordCandidates`. Zero load or zero reps raises nothing
    /// there, so it contests nothing here.
    ///
    /// The deepest rep max comes first among the rep maxes because it is the set's own: a set of
    /// six that beats the two through six is a six-rep max that also beat the shorter counts.
    private static func contested(by lift: SetCommand.Lift) -> [(RecordKind, Double)] {
        guard lift.weight > 0, lift.reps > 0 else { return [] }
        let repMaxes = RecordKind.repMaxes(heldBySetOf: lift.reps).reversed().map { ($0, lift.weight) }
        return [(.heaviestSet, lift.weight)] + repMaxes
            + [(.bestSetVolume, lift.weight * Double(lift.reps))]
    }

    /// The best figure for each record kind among the earlier sets that compete with `lift`.
    private static func bestFigures(
        in earlier: [SetCommand], competingWith lift: SetCommand.Lift
    ) -> [RecordKind: Double] {
        earlier
            .compactMap(\.liftPart)
            .filter { $0.kind.isWork && $0.movementDataId == lift.movementDataId && $0.unit == lift.unit }
            .flatMap(contested(by:))
            .reduce(into: [:]) { best, contest in
                let (kind, figure) = contest
                best[kind] = max(best[kind] ?? figure, figure)
            }
    }

    /// The figure the standing record of `kind` is ranked by, if one stands.
    private static func standingFigure(of kind: RecordKind, in standing: [PersonalRecord]) -> Double? {
        standing
            .first { $0.achievement.kind == kind }
            .flatMap { record in
                switch record.achievement {
                case .heaviestSet(let lift), .repMax(_, let lift): lift.weight
                case .bestSetVolume(let set): set.volume
                case .bestSessionVolume, .longestDistance, .fastestTime, .bestPace, .unrecognised:
                    nil
                }
            }
    }
}
