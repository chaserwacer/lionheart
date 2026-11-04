import Foundation

/// One record, as the server's `PersonalRecordView` sends it.
///
/// The movement is not embedded: the client already holds the catalogue, so a record carries its
/// subject and the screen names it.
///
/// `achievedAt` is **the instant the session was performed**, not when the row was written — a
/// session logged a week late sets a record dated the day it was lifted, which is the date a
/// lifter means by "when did I set that".
struct PersonalRecord: Equatable, Sendable, Identifiable {
    let personalRecordId: UUID
    let subject: RecordSubject
    let achievement: Achievement
    let achievedAt: Date
    let lineage: Lineage
    let standing: Standing

    var id: UUID { personalRecordId }

    /// This record's identity — what it shares with every record it beat and that beat it.
    var key: RecordKey {
        RecordKey(subject: subject, kind: achievement.kind, heldIn: achievement.heldIn)
    }

    /// What this record replaced.
    ///
    /// Two cases rather than an optional date, because they read differently: a first record is
    /// "first on record", which is not a predecessor that stood for zero days.
    enum Lineage: Equatable, Sendable {
        case firstOfItsKind
        /// The predecessor's own date, so how long it stood answers without a second read.
        case replaced(previousAchievedAt: Date)
    }

    /// Whether a record stands, was beaten, or was withdrawn.
    ///
    /// Three states rather than a flag: beaten is the athlete's own progress and retracted is a
    /// number they said was never real, and a history that cannot tell them apart cannot say which
    /// happened.
    enum Standing: Equatable, Sendable {
        case standing
        case beaten
        case retracted
        case unrecognised(String)

        init(wire: String?) {
            switch wire {
            case "Standing": self = .standing
            case "Beaten": self = .beaten
            case "Retracted": self = .retracted
            default: self = .unrecognised(wire ?? "")
            }
        }
    }

    /// Whether the athlete can withdraw this record. Only the standing record can be: retracting
    /// walks a chain back one link at a time, so history stays a record of what actually stood. A
    /// standing this build does not recognise is not offered — the server would be the one to find
    /// out what it meant.
    var isRetractable: Bool {
        switch standing {
        case .standing: true
        case .beaten, .retracted, .unrecognised: false
        }
    }

    /// What retracting this record asks first. The warning depends on the lineage, because the
    /// consequence does: a record that beat something puts it back, a first record leaves nothing.
    var retractionConfirmation: Confirmation {
        let consequence = switch lineage {
        case .replaced: "The record it beat stands again."
        case .firstOfItsKind: "It was the first of its kind, so nothing will stand in its place."
        }
        return Confirmation(
            question: "Retract this record?",
            warning: "Only for a set that was not real. \(consequence) This cannot be undone.",
            proceed: "Retract record")
    }
}

extension PersonalRecord: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let personalRecordId = container.lenient(UUID.self, "personalRecordId"),
              let achievedAt = container.instant("achievedAt")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A personal record needs its id and the instant it was set."))
        }

        self.init(
            personalRecordId: personalRecordId,
            subject: RecordSubject(
                wire: container.lenient(UUID.self, "movementDataId"), subject: container.lenient(String.self, "subject")),
            achievement: container.lenient(Achievement.self, "achievement") ?? .unrecognised(""),
            achievedAt: achievedAt,
            lineage: container.instant("previousAchievedAt")
                .map(Lineage.replaced(previousAchievedAt:)) ?? .firstOfItsKind,
            standing: Standing(wire: container.lenient(String.self, "standing")))
    }
}
