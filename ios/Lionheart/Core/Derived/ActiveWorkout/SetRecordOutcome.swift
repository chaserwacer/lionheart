import Foundation

/// Whether a set just logged beat a record standing against its movement.
///
/// A lift competes for three records at once, the same three the server's detector stages for a
/// set: the heaviest set, the rep max at its own rep count, and the best set volume. Beating any
/// of them is news the athlete wants while the bar is still loaded — "that was a five-rep max" is
/// the sentence the rep-max grid exists for.
///
/// What is deliberately not said here:
///
/// - **Records the set would be the first of.** A movement's first rep max at seven, or its first
///   record in a unit the athlete just switched to, is a record the server will set — but saying
///   so on every new rep count, or on every set of the first workout in kilograms, would make the
///   badge mean nothing by the time it was earned. Only beating a standing record is news.
/// - **Best session volume, and the distance records.** Those are decided over a whole session or
///   a whole effort, which the server reads on completion; the device would have to re-derive its
///   distance conversions and paces to guess at them, and a guess that disagrees with the record
///   that lands is worse than silence.
///
/// The server remains the authority. These records come from the summaries as they stood when the
/// workout began, so two sets beating the same record both read as beating it; deciding which of
/// them *became* the record is the server's job when the session completes.
enum SetRecordOutcome: Equatable, Sendable {
    /// Not a lift, so nothing to say mid-workout.
    case notApplicable
    /// No record stands in this set's unit for any of the three it competes for.
    case noRecordYet
    case none
    /// The records it beat, in the order they are worth saying: heaviest set, rep max, set volume.
    case beats(RecordKind, also: [RecordKind])

    var isWorthSaying: Bool {
        switch self {
        case .notApplicable, .noRecordYet, .none: false
        case .beats: true
        }
    }

    /// Every record beaten, in the order they are worth saying.
    var beaten: [RecordKind] {
        switch self {
        case .notApplicable, .noRecordYet, .none: []
        case .beats(let first, let rest): [first] + rest
        }
    }

    /// - Note: Only records held in the set's own unit are compared. Weights never convert — 225 lb
    ///   is a larger number and a lighter lift than 110 kg — and the unit is part of a record's
    ///   identity, so a pounds set competes with the pounds records and a kilograms record is not
    ///   one it could beat.
    static func of(_ command: SetCommand, against held: SubjectRecords?) -> SetRecordOutcome {
        guard let lift = command.liftPart else { return .notApplicable }

        let standing = (held?.records ?? []).filter {
            $0.standing == .standing && $0.achievement.heldIn == lift.unit
        }
        let contests = contested(by: lift).compactMap { kind, figure in
            standingFigure(of: kind, in: standing).map { (kind, beats: figure > $0) }
        }

        guard !contests.isEmpty else { return .noRecordYet }
        let beaten = contests.filter(\.beats).map(\.0)
        guard let first = beaten.first else { return .none }
        return .beats(first, also: Array(beaten.dropFirst()))
    }

    /// The records a lift competes for, with its figure for each — mirroring the server's
    /// `RecordCandidates`. Zero load or zero reps raises nothing there, so it contests nothing
    /// here; and the rep-max grid stops at `RecordKind.highestGradedRepCount`.
    private static func contested(by lift: SetCommand.Lift) -> [(RecordKind, Double)] {
        guard lift.weight > 0, lift.reps > 0 else { return [] }
        let repMax: [(RecordKind, Double)] = lift.reps <= RecordKind.highestGradedRepCount
            ? [(.repMax(reps: lift.reps), lift.weight)]
            : []
        return [(.heaviestSet, lift.weight)] + repMax
            + [(.bestSetVolume, lift.weight * Double(lift.reps))]
    }

    /// The figure the standing record of `kind` is ranked by, if one stands.
    private static func standingFigure(of kind: RecordKind, in standing: [PersonalRecord]) -> Double? {
        standing
            .first { $0.achievement.kind == kind }
            .flatMap { record in
                switch record.achievement {
                case .heaviestSet(let lift), .repMax(let lift): lift.weight
                case .bestSetVolume(let set): set.volume
                case .bestSessionVolume, .longestDistance, .fastestTime, .bestPace, .unrecognised:
                    nil
                }
            }
    }
}
