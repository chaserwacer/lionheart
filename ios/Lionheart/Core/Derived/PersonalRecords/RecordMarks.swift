import Foundation

/// Which sets of a session set a record, read off the records the server established.
///
/// The server is the authority on what a set established — it decided when the session's sets
/// became final — so nothing here re-runs the detection. A record names no session or set; it
/// carries its session's start instant, its subject and the set as performed, and that is what
/// finds the set: the same movement, in a session that began at that instant, with that load,
/// those reps and that unit (or that distance and time for a run). Where two sets of one session
/// are identical, the first carries the mark.
///
/// Two kinds of record mark nothing, for the reason the live badge stays quiet about them
/// (`SetRecordOutcome`): the first of its kind on a subject, which every first session would
/// otherwise wear a dozen of, and one the athlete withdrew, which he said was never real. A beaten
/// record still marks its set — it was a record when it was set.
struct RecordMarks: Equatable, Sendable {
    private let bySet: [SetAddress: [RecordKind]]
    /// The movements whose best session volume a session set — a record held by the session's work
    /// on the movement as a whole rather than by one set.
    private let sessionVolumes: Set<SessionSubject>

    struct SessionSubject: Hashable, Sendable {
        let sessionId: UUID
        let subject: RecordSubject
    }

    /// One set: its session, and its place in that session's whole list.
    struct SetAddress: Hashable, Sendable {
        let sessionId: UUID
        let order: Int
    }

    /// The sets a session holds, as `of` reads them.
    struct Session: Sendable {
        let sessionId: UUID
        let startedAt: Date?
        let sets: [SessionDetail.IndexedSet]
    }

    var isEmpty: Bool { bySet.isEmpty && sessionVolumes.isEmpty }

    /// The records a set established, the most worth saying first.
    func kinds(of set: SetAddress) -> [RecordKind] { bySet[set] ?? [] }

    /// Whether the session set the best session volume on a subject.
    func setSessionVolume(in sessionId: UUID, on subject: RecordSubject) -> Bool {
        sessionVolumes.contains(SessionSubject(sessionId: sessionId, subject: subject))
    }

    /// How many movements a session set its best session volume on.
    func sessionVolumeCount(in sessionId: UUID) -> Int {
        sessionVolumes.filter { $0.sessionId == sessionId }.count
    }

    /// How many sets of a session carry a mark.
    func markedSetCount(in sessionId: UUID) -> Int {
        bySet.keys.filter { $0.sessionId == sessionId }.count
    }

    /// The mark as a set row draws it.
    func outcome(of set: SetAddress) -> SetRecordOutcome {
        let kinds = kinds(of: set)
        guard let first = kinds.first else { return .notApplicable }
        return .beats(first, also: Array(kinds.dropFirst()))
    }

    static func of(_ records: [PersonalRecord], in sessions: [Session]) -> RecordMarks {
        let marking = records.filter { record in
            switch (record.standing, record.lineage) {
            case (.retracted, _), (.unrecognised, _), (_, .firstOfItsKind): false
            case (.standing, .replaced), (.beaten, .replaced): true
            }
        }

        var bySet: [SetAddress: [RecordKind]] = [:]
        var sessionVolumes: Set<SessionSubject> = []

        for record in marking {
            guard let session = sessions.first(where: { began(at: record.achievedAt, $0) }) else { continue }
            if case .bestSessionVolume = record.achievement {
                sessionVolumes.insert(SessionSubject(sessionId: session.sessionId, subject: record.subject))
                continue
            }
            guard let set = session.sets.first(where: { holds(record, $0.entry) }) else { continue }
            bySet[SetAddress(sessionId: session.sessionId, order: set.order), default: []]
                .append(record.achievement.kind)
        }

        return RecordMarks(
            bySet: bySet.mapValues { $0.sorted { rank($0) < rank($1) } },
            sessionVolumes: sessionVolumes)
    }

    /// Whether a session began at a record's instant. The two travel as text with different
    /// precision, so a second either way is the same instant.
    private static func began(at instant: Date, _ session: Session) -> Bool {
        session.startedAt.map { abs($0.timeIntervalSince(instant)) < 1 } ?? false
    }

    /// Whether a set is the performance a record holds.
    private static func holds(_ record: PersonalRecord, _ entry: SetEntry) -> Bool {
        switch record.achievement {
        case .heaviestSet(let held), .repMax(_, let held):
            liftHolds(record.subject, weight: held.weight, reps: held.reps, unit: held.unit, entry)
        case .bestSetVolume(let held):
            liftHolds(record.subject, weight: held.weight, reps: held.reps, unit: held.unit, entry)
        case .longestDistance(let effort), .fastestTime(_, let effort):
            effortHolds(record.subject, effort, entry)
        case .bestPace(let pace):
            effortHolds(record.subject, pace.effort, entry)
        case .bestSessionVolume, .unrecognised:
            false
        }
    }

    private static func liftHolds(
        _ subject: RecordSubject, weight: Double, reps: Int, unit: WeightUnit, _ entry: SetEntry
    ) -> Bool {
        guard case .lift(let lift) = entry else { return false }
        return subject == .movement(lift.movementDataId)
            && lift.weight == weight && lift.reps == reps && lift.unit == unit
    }

    private static func effortHolds(
        _ subject: RecordSubject, _ effort: Achievement.Distance, _ entry: SetEntry
    ) -> Bool {
        switch entry {
        case .distanceTime(let set):
            subject == .movement(set.movementDataId)
                && same(effort, distance: set.distance, unit: set.unit, duration: set.duration)
        case .trailRun(let run):
            subject == .trailRunning
                && same(effort, distance: run.distance, unit: run.unit, duration: run.duration)
        case .endurance(let set):
            subject == set.activity.recordSubject
                && same(effort, distance: set.distance, unit: set.unit, duration: set.duration)
        case .lift, .freeform, .unrecognised:
            false
        }
    }

    private static func same(
        _ effort: Achievement.Distance, distance: Double, unit: DistanceUnit, duration: TimeInterval
    ) -> Bool {
        effort.distance == distance && effort.unit == unit && abs(effort.duration - duration) < 1
    }

    /// The order a set's marks are said in, matching the live badge: the heaviest set, the rep
    /// maxes from the highest count down, the set volume, then the distance records.
    private static func rank(_ kind: RecordKind) -> Double {
        switch kind {
        case .heaviestSet: 0
        case .repMax(let reps): 1 + Double(100 - reps) / 1000
        case .bestSetVolume: 2
        case .longestDistance: 3
        case .fastestTime: 4
        case .bestPace: 5
        case .bestSessionVolume, .unrecognised: 6
        }
    }
}
