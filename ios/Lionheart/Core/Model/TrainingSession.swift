import Foundation

/// Whether a record came from the athlete or was assisted by the model. Orthogonal to lifecycle.
enum Provenance: Equatable, Sendable {
    case user
    case aiAssisted
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .user
        case 1: self = .aiAssisted
        default: self = .unrecognised(wire ?? -1)
        }
    }
}

/// One bout of training, as the server's sealed lifecycle.
///
/// Four cases with different fields, not one shape with a status: prescriptions exist only on a
/// planned session, a start instant only on an active one, sets and effort only where they were
/// actually performed. Flattening this into a struct with a `status` string and a bag of optionals
/// is the defect the backend's remodel exists to prevent, and the client is not allowed to undo it.
///
/// `envelope` holds what every state carries. The date in it is the server's own derivation at the
/// offset the session was logged at, which is why it is received rather than computed here.
enum TrainingSession: Equatable, Sendable, Identifiable {
    case planned(Planned)
    case active(Active)
    case completed(Completed)
    case skipped(Skipped)
    /// A lifecycle state this client has not heard of. It keeps the envelope, so the session still
    /// appears in a list and still has a date and an id to act on.
    case unrecognised(type: String, envelope: Envelope)

    /// What a session carries in every state.
    struct Envelope: Equatable, Sendable {
        let trainingSessionId: UUID
        let notes: String
        let provenance: Provenance
        /// The program this session belongs to, or `nil` when it stands alone. The server models
        /// membership as a fill-state; on the wire it is this nullable id.
        let trainingProgramId: UUID?
        /// The routine the session was trained from, or `nil` for an ad hoc one. The server's
        /// fill-state is this nullable id on the wire, like program membership.
        let routineId: UUID?
        /// Where the session came from — the athlete's log, or a run imported from Apple Health.
        let source: SessionSource
        let createdAt: Date?
        /// The calendar day the session belongs to, derived server-side by each state from its own
        /// time-truth.
        let date: CalendarDay

        init(
            trainingSessionId: UUID, notes: String, provenance: Provenance, trainingProgramId: UUID?,
            routineId: UUID? = nil, source: SessionSource = .athleteLog, createdAt: Date?, date: CalendarDay
        ) {
            self.trainingSessionId = trainingSessionId
            self.notes = notes
            self.provenance = provenance
            self.trainingProgramId = trainingProgramId
            self.routineId = routineId
            self.source = source
            self.createdAt = createdAt
            self.date = date
        }
    }

    struct Planned: Equatable, Sendable {
        let envelope: Envelope
        let plannedFor: CalendarDay
        let prescriptions: [SetPrescription]
    }

    struct Active: Equatable, Sendable {
        let envelope: Envelope
        let startedAt: Date
        let sets: [SetEntry]
    }

    struct Completed: Equatable, Sendable {
        let envelope: Envelope
        let sets: [SetEntry]
        let period: Period
        let effort: EffortState
        let decoration: DecorationState
    }

    struct Skipped: Equatable, Sendable {
        let envelope: Envelope
        let plannedFor: CalendarDay
    }

    var envelope: Envelope {
        switch self {
        case .planned(let session): session.envelope
        case .active(let session): session.envelope
        case .completed(let session): session.envelope
        case .skipped(let session): session.envelope
        case .unrecognised(_, let envelope): envelope
        }
    }

    var id: UUID { envelope.trainingSessionId }
    var date: CalendarDay { envelope.date }

    /// The sets performed so far, which only the two states that have performed any can offer.
    var sets: [SetEntry] {
        switch self {
        case .active(let session): session.sets
        case .completed(let session): session.sets
        case .planned, .skipped, .unrecognised: []
        }
    }
}

extension TrainingSession: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        guard let trainingSessionId = container.lenient(UUID.self, "trainingSessionId"),
              let date = container.day("date")
        else {
            // Without an id and a day there is no session to show and nothing to act on, so this
            // is the one place a row is genuinely undecodable. `lenientList` drops it and keeps
            // the rest of the window.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A training session needs both a trainingSessionId and a date."))
        }

        let envelope = Envelope(
            trainingSessionId: trainingSessionId,
            notes: container.lenient("notes", or: ""),
            provenance: Provenance(wire: container.lenient(Int.self, "provenance")),
            trainingProgramId: container.lenient(UUID.self, "trainingProgramId"),
            routineId: container.lenient(UUID.self, "routineId"),
            source: container.lenient(SessionSource.self, "source") ?? .athleteLog,
            createdAt: container.instant("createdAt"),
            date: date)

        switch type {
        case "planned":
            self = .planned(Planned(
                envelope: envelope,
                plannedFor: container.day("plannedFor") ?? date,
                prescriptions: container.lenientList(SetPrescription.self, "prescriptions")))

        case "active":
            guard let startedAt = container.instant("startedAt") else {
                self = .unrecognised(type: type, envelope: envelope)
                return
            }
            self = .active(Active(
                envelope: envelope,
                startedAt: startedAt,
                sets: container.lenientList(SetEntry.self, "sets")))

        case "completed":
            self = .completed(Completed(
                envelope: envelope,
                sets: container.lenientList(SetEntry.self, "sets"),
                period: container.lenient(Period.self, "period") ?? .unrecognised(""),
                effort: container.lenient(EffortState.self, "effort") ?? .notReported,
                decoration: container.lenient(DecorationState.self, "decoration") ?? .none))

        case "skipped":
            self = .skipped(Skipped(
                envelope: envelope,
                plannedFor: container.day("plannedFor") ?? date))

        default:
            self = .unrecognised(type: type, envelope: envelope)
        }
    }
}
