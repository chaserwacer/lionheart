import Foundation

/// Every record standing on one subject — the server's `SubjectRecordsView`.
///
/// A list rather than a slot per kind: a movement with a rep-max grid holds a dozen or more
/// standing records, and two records of one kind differing only in weight unit are both in it.
/// The server orders it newest first, then by kind and bracket; screens arrange it for themselves
/// through `Core/Derived/PersonalRecords/`.
struct SubjectRecords: Equatable, Sendable, Identifiable {
    let subject: RecordSubject
    let records: [PersonalRecord]
    let lastAchievedAt: Date?

    var id: RecordSubject { subject }

    var isEmpty: Bool { records.isEmpty }
}

extension SubjectRecords: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        self.init(
            subject: RecordSubject(wire: container.lenient(UUID.self, "movementDataId")),
            records: container.lenientList(PersonalRecord.self, "records"),
            lastAchievedAt: container.instant("lastAchievedAt"))
    }
}
