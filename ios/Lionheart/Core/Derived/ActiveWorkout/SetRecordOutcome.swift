import Foundation

/// Whether a set beat the records standing against its movement.
///
/// Mirrors the server's two `PersonalRecordType` cases exactly: **Strength** compares the weight
/// alone, **Volume** compares weight × reps. Both are strict — equalling a record does not beat it,
/// which is also what keeps a repeated set from announcing itself five times.
///
/// It is derived on the device so the news arrives when the set is logged rather than when the
/// session is pushed, which is the whole point of showing it. The server remains the authority:
/// these records come from `GET /api/personal-records/summaries` as they stood when the workout
/// started, so two sets beating the same record in one workout both read as beating it. Correcting
/// that would mean the device deciding which of them *became* the record, which is the server's job.
enum SetRecordOutcome: Equatable, Sendable {
    /// Not a shape that has records. Only a lift does — the server tracks none for distance.
    case notApplicable
    /// There is no record for this movement to beat.
    case noRecordYet
    case none
    case strength
    case volume
    case both

    /// Whether this is worth telling the athlete about.
    ///
    /// `.noRecordYet` is deliberately silent. A new athlete logging their first ever set has not
    /// achieved anything by having no history, and announcing it on every set of their first weeks
    /// would make the badge mean nothing by the time it was earned.
    var isWorthSaying: Bool {
        switch self {
        case .notApplicable, .noRecordYet, .none: false
        case .strength, .volume, .both: true
        }
    }

    /// - Note: A record held in a different unit from the set is **not** compared. The server
    ///   stores a record's unit and compares weights numerically without converting, so a
    ///   device-side conversion would announce a record the server then disagrees with — 225 lb
    ///   would "beat" a 100 kg record. Silence is the honest answer until the athlete's units
    ///   agree again, and it is the same reasoning that keeps volume totals unconverted.
    static func of(_ command: SetCommand, against records: MovementRecords?) -> SetRecordOutcome {
        guard let lift = command.liftPart else { return .notApplicable }
        guard let records, !records.isEmpty else { return .noRecordYet }

        func comparable(_ record: PersonalRecord?) -> PersonalRecord? {
            guard let record, record.unit == lift.unit else { return nil }
            return record
        }

        let beatsWeight = comparable(records.strength).map { lift.weight > $0.weight } ?? false
        let beatsVolume = comparable(records.volume)
            .map { lift.weight * Double(lift.reps) > $0.volume } ?? false

        switch (beatsWeight, beatsVolume) {
        case (true, true): return .both
        case (true, false): return .strength
        case (false, true): return .volume
        case (false, false): return .none
        }
    }
}
