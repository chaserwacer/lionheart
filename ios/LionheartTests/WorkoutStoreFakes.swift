import Foundation

@testable import Lionheart

/// A server that accepts everything, for suites whose claims are not about sync.
///
/// Shared by the Live Activity and rest-alarm suites, which both need a store that can reach the
/// end of its completing and discarding paths without the server being the subject. The suite in
/// `ActiveWorkoutStoreTests` keeps its own, richer recorder, because there the server *is* the
/// subject.
struct AnAgreeableServer: WorkoutSessionWriting {
    func session(id: UUID) async throws -> TrainingSession { Self.active(id) }

    func sessions(from: Date, to: Date) async throws -> [TrainingSession] { [] }

    func start(id: UUID, at startedAt: Date) async throws -> TrainingSession { Self.active(id) }

    func startUnplanned(
        at startedAt: Date, notes: String, programId: UUID?, routineId: UUID?
    ) async throws -> TrainingSession {
        Self.active(AWorkout.session)
    }

    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession {
        Self.active(id)
    }

    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession { Self.active(id) }

    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession {
        Self.active(id)
    }

    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String,
        programId: UUID?,
        routineId: UUID?
    ) async throws -> TrainingSession {
        Self.active(AWorkout.session)
    }

    func delete(id: UUID) async throws {}

    private static func active(_ id: UUID) -> TrainingSession {
        .active(TrainingSession.Active(
            envelope: TrainingSession.Envelope(
                trainingSessionId: id,
                notes: "",
                provenance: .user,
                trainingProgramId: nil,
                createdAt: nil,
                date: CalendarDay(year: 2026, month: 11, day: 2)),
            startedAt: AWorkout.start,
            sets: []))
    }
}

/// A server that answers however a test needs it to.
///
/// It stands in for the one collaborator a unit test cannot run. It records what it was asked to
/// write, because many of the claims about the workout are about *what reached the server* rather
/// than about what the store returned.
///
/// `@unchecked` because its calls arrive from the store's requests, off the main actor, but never
/// two at once: the store awaits each request before sending the next (the one place two could
/// overlap, opening a deferred start, is exactly what the store joins), and the tests read it only
/// after awaiting the store.
final class ARecordingServer: WorkoutSessionWriting, @unchecked Sendable {
    /// The id the server gives a session it opens for an unplanned start.
    static let opened = UUID(uuidString: "99999999-9999-9999-9999-999999999999")!

    private(set) var pushedSets: [[SetCommand]] = []
    private(set) var completions = 0
    private(set) var logged: [[SetCommand]] = []
    private(set) var notes: [String] = []
    private(set) var deletions: [UUID] = []
    private(set) var efforts: [PerceivedEffortRequest?] = []
    private(set) var plannedStarts: [UUID] = []
    private(set) var unplannedStarts: [Date] = []
    /// The routine each unplanned start and each whole-workout log named.
    private(set) var unplannedRoutines: [UUID?] = []
    private(set) var loggedRoutines: [UUID?] = []
    private(set) var windowReads = 0

    var failPushes: (any Error)?
    var failCompletion: (any Error)?
    /// Makes both kinds of start fail.
    var failOpening: (any Error)?
    /// How long an unplanned start takes to answer, so a test can overlap two.
    var openingTakes: Duration = .zero
    /// How long a read of the sessions around a start takes, so a test can act while one is out.
    var windowReadTakes: Duration = .zero
    /// Failures for the next whole-workout logs, one each, in order; the ones after succeed.
    var failNextLogs: [any Error] = []
    /// Makes a planned start land on the server and answer a refusal all the same — what a
    /// device sees when its own earlier start landed after it stopped waiting for the answer.
    var plannedStartLandsRefused = false
    /// Makes a read of the session answer that it is gone.
    var sessionIsGone = false

    /// What a read of the session answers. `nil` makes the read itself fail, which is the case the
    /// store must treat as "I cannot tell" rather than as "not completed".
    var sessionOnServer: TrainingSession?
    private(set) var sessionReads: [UUID] = []

    /// What a read of the sessions around a start answers. `nil` makes it fail as unreachable.
    var sessionsAround: [TrainingSession]? = []

    func session(id: UUID) async throws -> TrainingSession {
        sessionReads.append(id)
        if sessionIsGone { throw APIError.notFound(detail: nil) }
        guard let sessionOnServer else { throw APIError.transport("the read itself failed") }
        return sessionOnServer
    }

    func sessions(from: Date, to: Date) async throws -> [TrainingSession] {
        windowReads += 1
        try await Task.sleep(for: windowReadTakes)
        guard let sessionsAround else { throw APIError.transport("The network is not reachable.") }
        return sessionsAround
    }

    func start(id: UUID, at startedAt: Date) async throws -> TrainingSession {
        plannedStarts.append(id)
        if plannedStartLandsRefused {
            sessionOnServer = Self.anActiveSession(id: id, startedAt: startedAt)
            throw APIError.refused(detail: "A active session cannot be started.")
        }
        if let failOpening { throw failOpening }
        return Self.anActiveSession(id: id, startedAt: startedAt)
    }

    func startUnplanned(
        at startedAt: Date, notes: String, programId: UUID?, routineId: UUID?
    ) async throws -> TrainingSession {
        try await Task.sleep(for: openingTakes)
        unplannedStarts.append(startedAt)
        unplannedRoutines.append(routineId)
        if let failOpening { throw failOpening }
        return Self.anActiveSession(id: Self.opened, startedAt: startedAt)
    }

    func replaceSets(id: UUID, with sets: [SetCommand]) async throws -> TrainingSession {
        pushedSets.append(sets)
        if let failPushes { throw failPushes }
        return Self.anActiveSession(id: id)
    }

    func updateNotes(id: UUID, to notes: String) async throws -> TrainingSession {
        self.notes.append(notes)
        if let failCompletion { throw failCompletion }
        return Self.anActiveSession(id: id)
    }

    func complete(
        id: UUID, period: PeriodRequest, effort: PerceivedEffortRequest?
    ) async throws -> TrainingSession {
        if let failCompletion { throw failCompletion }
        efforts.append(effort)
        completions += 1
        return Self.anActiveSession(id: id)
    }

    func logCompleted(
        sets: [SetCommand],
        period: PeriodRequest,
        effort: PerceivedEffortRequest?,
        notes: String,
        programId: UUID?,
        routineId: UUID?
    ) async throws -> TrainingSession {
        if let failCompletion { throw failCompletion }
        if !failNextLogs.isEmpty { throw failNextLogs.removeFirst() }
        efforts.append(effort)
        logged.append(sets)
        loggedRoutines.append(routineId)
        return Self.anActiveSession(id: AWorkout.session)
    }

    func delete(id: UUID) async throws {
        deletions.append(id)
    }

    /// - Parameter setCount: how many sets it holds, which is part of how a finished workout is
    ///   recognised among the server's sessions.
    static func aCompletedSession(
        id: UUID, startedAt: Date = AWorkout.start, setCount: Int = 0
    ) -> TrainingSession {
        .completed(TrainingSession.Completed(
            envelope: anEnvelope(id),
            sets: Array(
                repeating: .freeform(SetEntry.Common(source: .formed, facets: [])), count: setCount),
            period: .instant(at: startedAt),
            effort: .notReported,
            decoration: .none))
    }

    static func anActiveSession(id: UUID, startedAt: Date = AWorkout.start) -> TrainingSession {
        .active(TrainingSession.Active(envelope: anEnvelope(id), startedAt: startedAt, sets: []))
    }

    static func aPlannedSession(id: UUID) -> TrainingSession {
        .planned(TrainingSession.Planned(
            envelope: anEnvelope(id),
            plannedFor: CalendarDay(year: 2026, month: 11, day: 2),
            prescriptions: []))
    }

    static func aSkippedSession(id: UUID) -> TrainingSession {
        .skipped(TrainingSession.Skipped(
            envelope: anEnvelope(id), plannedFor: CalendarDay(year: 2026, month: 11, day: 2)))
    }

    private static func anEnvelope(_ id: UUID) -> TrainingSession.Envelope {
        TrainingSession.Envelope(
            trainingSessionId: id,
            notes: "",
            provenance: .user,
            trainingProgramId: nil,
            createdAt: nil,
            date: CalendarDay(year: 2026, month: 11, day: 2))
    }
}

/// An outbox writing into a directory of its own, so no two tests can see each other's finished
/// workouts and none of them touches the app's real one.
@MainActor
func anOutbox(sending sessions: any WorkoutSessionWriting) -> (WorkoutOutbox, FinishedWorkoutsDocument) {
    let document = FinishedWorkoutsDocument(
        directoryURL: URL.temporaryDirectory.appending(path: UUID().uuidString))
    return (WorkoutOutbox(document: document, sessions: sessions), document)
}
