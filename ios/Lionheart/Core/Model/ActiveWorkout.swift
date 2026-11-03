import Foundation

/// Where the sets of a workout are going to land on the server.
///
/// Two cases because the two are genuinely different transactions, not one with a flag. A live
/// workout has an `ActiveTrainingSession` already on the server and pushes its set list as the
/// athlete logs, so nothing is riding on the device for longer than a coalescing window. A workout
/// being written down afterwards has nothing on the server at all until it is finished, and then it
/// lands whole through `POST /completed` — which is also why it works with no signal from first set
/// to last.
enum WorkoutDestination: Equatable, Sendable {
    /// The id of the active session this workout's sets replace on every push.
    case liveSession(UUID)
    /// Nothing on the server yet. `POST /completed` carries the sets, the period and the effort in
    /// one request when the athlete finishes.
    case afterTheFact

    var sessionId: UUID? {
        switch self {
        case .liveSession(let id): id
        case .afterTheFact: nil
        }
    }

    /// Whether logging a set should try to reach the server at all.
    var pushesAsItGoes: Bool {
        switch self {
        case .liveSession: true
        case .afterTheFact: false
        }
    }
}

/// Whether what has been logged has reached the server.
///
/// Four states, not a `Bool`, because they are four different things to say to an athlete and only
/// one of them asks anything of them. `.pending` is ordinary and silent, `.failed` is reassuring
/// (the next push carries everything), and `.refused` is the one that needs them: the server will
/// not take these sets, so pushing again cannot help.
///
/// `.refused` is a 409 — the session is no longer active, because it was completed or deleted
/// somewhere else. It is a domain refusal and the bar states it rather than retrying it.
enum WorkoutSyncState: Equatable, Sendable {
    case synced
    /// Logged locally and not pushed yet, or a push is in flight.
    case pending
    /// The last push did not reach the server. The next one carries everything.
    case failed(String)
    /// The server will not accept these sets. Retrying is not what helps.
    case refused(String)

    /// Whether another push could plausibly land this. `.refused` cannot, which is the distinction
    /// the case exists for.
    var isWorthPushing: Bool {
        switch self {
        case .synced: false
        case .pending, .failed: true
        case .refused: false
        }
    }

    /// Whether the athlete has to know. `.pending` is the state almost all logging is in for a
    /// second or two, and a warning for it would cry wolf.
    var needsSaying: Bool {
        switch self {
        case .synced, .pending: false
        case .failed, .refused: true
        }
    }
}

/// The in-flight workout as the device holds it.
///
/// The one piece of state Lionheart owns rather than reads, because the gym is where the signal is
/// worst and a set lost to a force-quit is the single failure that would stop the owner using the
/// app. It is a value, and `ActiveWorkoutStore` writes the whole of it to disk on every change —
/// which is only safe to do cheaply because it is small and immutable.
///
/// It carries the prescriptions of the session it was started from, rather than re-reading them,
/// for the same reason: the targets have to be on screen in a basement.
///
/// `version` is what makes a stored document safe to meet. A document from an older build is read
/// with the members it lacks defaulted; one from a *newer* build is refused whole rather than
/// half-read, because a downgraded build that half-understood a future shape would write it back
/// truncated, which is how logged sets get lost for good.
struct ActiveWorkout: Equatable, Sendable {
    /// The shape of the persisted document.
    ///
    /// 1 — the foundation's: a session id, a start instant and a synced flag.
    /// 2 — a destination rather than a bare id, the logged sets, the rest clock, the current
    ///     movement, the prescriptions, and sync as a four-case state.
    /// 3 — this one: the movements the athlete added, so one picked before any set exists is part
    ///     of the workout.
    static let currentVersion = 3

    let version: Int
    let destination: WorkoutDestination
    let startedAt: Date
    let sync: WorkoutSyncState
    /// The sets logged so far, in the order they were performed. The server's list is replaced
    /// with exactly this, so its order is the record.
    let sets: [LoggedSet]
    let rest: RestClock
    /// The movement the athlete is working through. It is the one the entry row is open on, which
    /// is why it is held rather than derived from the last set: moving to the next movement happens
    /// before the first set on it exists.
    let currentMovementDataId: UUID?
    /// The targets of the planned session this was started from. Empty for an unplanned workout.
    let prescriptions: [SetPrescription]
    /// The movements the athlete added themselves, in the order they added them.
    ///
    /// Held because choosing a movement is the athlete saying what they are about to train, and
    /// that is true before the first set on it exists. Neither the sets nor the prescriptions can
    /// say it, so without this an unplanned workout has nowhere to log its first lift.
    let addedMovementDataIds: [UUID]
    /// What the athlete typed about the session. Sent with `POST /completed`; for a live session it
    /// rides along to `PATCH /notes`.
    let notes: String

    init(
        destination: WorkoutDestination,
        startedAt: Date,
        sync: WorkoutSyncState = .synced,
        sets: [LoggedSet] = [],
        rest: RestClock = .idle,
        currentMovementDataId: UUID? = nil,
        prescriptions: [SetPrescription] = [],
        addedMovementDataIds: [UUID] = [],
        notes: String = "",
        version: Int = Self.currentVersion
    ) {
        self.version = version
        self.destination = destination
        self.startedAt = startedAt
        self.sync = sync
        self.sets = sets
        self.rest = rest
        self.currentMovementDataId = currentMovementDataId
        self.prescriptions = prescriptions
        self.addedMovementDataIds = addedMovementDataIds
        self.notes = notes
    }

    /// How long the session has been running at `now`. Never negative, so a device clock that moved
    /// backwards cannot show a workout counting down.
    func elapsed(at now: Date) -> TimeInterval {
        max(0, now.timeIntervalSince(startedAt))
    }

    /// Whether a document read from disk is one this build understands. See the type's note.
    var isReadable: Bool { version <= Self.currentVersion }

    // MARK: - Changes

    /// The workout with `set` appended and the rest clock started, as one step.
    ///
    /// They are one step on purpose: a logged set and the rest that follows it are a single thing
    /// the athlete did, and persisting them separately would leave a window in which the set is
    /// saved with the previous set's rest still running.
    func appending(_ set: LoggedSet, restingFor rest: TimeInterval?) -> ActiveWorkout {
        // A set with no movement of its own — a trail run, a set logged as words — leaves the
        // athlete where they were, so the movement only moves when the new set names one.
        with(
            sync: .pending,
            sets: sets + [set],
            rest: rest.map { .running(since: set.loggedAt, duration: $0) } ?? .idle)
            .working(on: set.movementDataId ?? currentMovementDataId)
    }

    /// The workout with the set of this id restated. Unknown ids change nothing, so a stale row
    /// tapped twice cannot append a duplicate.
    func restating(_ id: UUID, as command: SetCommand) -> ActiveWorkout {
        guard sets.contains(where: { $0.id == id }) else { return self }
        return with(
            sync: .pending,
            sets: sets.map { $0.id == id ? $0.restated(as: command) : $0 })
    }

    func removing(_ id: UUID) -> ActiveWorkout {
        guard sets.contains(where: { $0.id == id }) else { return self }
        return with(sync: .pending, sets: sets.filter { $0.id != id })
    }

    func marking(_ sync: WorkoutSyncState) -> ActiveWorkout {
        with(sync: sync)
    }

    func resting(_ rest: RestClock) -> ActiveWorkout {
        with(rest: rest)
    }

    func working(on movementDataId: UUID?) -> ActiveWorkout {
        // Passing nil through `with` would read as "leave it alone", so the whole value is rebuilt
        // here. Clearing the current movement is a thing the surface does when the athlete closes
        // the entry row.
        ActiveWorkout(
            destination: destination,
            startedAt: startedAt,
            sync: sync,
            sets: sets,
            rest: rest,
            currentMovementDataId: movementDataId,
            prescriptions: prescriptions,
            addedMovementDataIds: addedMovementDataIds,
            notes: notes,
            version: version)
    }

    /// The workout with `movementDataId` added, and the athlete working on it.
    ///
    /// A movement the workout already holds — prescribed, logged on, or added before — only moves
    /// the athlete to it. Its section stays where it is, because a pick is a way back to a movement
    /// as much as a way to a new one, and sections jumping about mid-workout lose the athlete's
    /// place.
    func adding(_ movementDataId: UUID) -> ActiveWorkout {
        guard !holds(movementDataId) else { return working(on: movementDataId) }
        return with(addedMovementDataIds: addedMovementDataIds + [movementDataId])
            .working(on: movementDataId)
    }

    /// Whether the athlete can take this movement back out: they added it, nothing was prescribed
    /// for it and nothing has been logged on it.
    ///
    /// A set is the record that a movement was trained and a prescription is the plan, and neither
    /// is something taking back a pick can undo. This is the one statement of that rule — the
    /// surface asks it before offering Remove, and `dropping(_:)` asks it before doing anything.
    func canDrop(_ movementDataId: UUID) -> Bool {
        addedMovementDataIds.contains(movementDataId)
            && !sets.contains { $0.movementDataId == movementDataId }
            && !prescriptions.contains { $0.movementDataId == movementDataId }
    }

    /// The workout without a movement the athlete added by mistake. Anything `canDrop(_:)` refuses
    /// is returned unchanged.
    func dropping(_ movementDataId: UUID) -> ActiveWorkout {
        guard canDrop(movementDataId) else { return self }

        let dropped = with(addedMovementDataIds: addedMovementDataIds.filter { $0 != movementDataId })
        return currentMovementDataId == movementDataId ? dropped.working(on: nil) : dropped
    }

    /// Whether the movement already has a section: prescribed, logged on, or added.
    private func holds(_ movementDataId: UUID) -> Bool {
        addedMovementDataIds.contains(movementDataId)
            || prescriptions.contains { $0.movementDataId == movementDataId }
            || sets.contains { $0.movementDataId == movementDataId }
    }

    func noting(_ notes: String) -> ActiveWorkout {
        with(notes: notes)
    }

    /// The workout with a different start.
    ///
    /// Only meaningful for one being written down afterwards, where the athlete states when it
    /// began. A live session's start is the server's, stamped when it created the session, and
    /// moving it on the device would make the period sent disagree with the session it completes.
    func starting(at startedAt: Date) -> ActiveWorkout {
        ActiveWorkout(
            destination: destination,
            startedAt: startedAt,
            sync: sync,
            sets: sets,
            rest: rest,
            currentMovementDataId: currentMovementDataId,
            prescriptions: prescriptions,
            addedMovementDataIds: addedMovementDataIds,
            notes: notes,
            version: version)
    }

    /// The workout with some members replaced and the rest kept.
    ///
    /// `currentMovementDataId` is deliberately **not** a parameter, and `working(on:)` is the only
    /// way it changes. A doubly-optional "not supplied" parameter for it compiled and was wrong:
    /// `??` resolves against the outer optional, so passing a set that genuinely has no movement
    /// cleared the current one instead of leaving the athlete where they were. There is no sentinel
    /// that reads as "leave this alone" for a value whose own absence is meaningful.
    private func with(
        sync: WorkoutSyncState? = nil,
        sets: [LoggedSet]? = nil,
        rest: RestClock? = nil,
        addedMovementDataIds: [UUID]? = nil,
        notes: String? = nil
    ) -> ActiveWorkout {
        ActiveWorkout(
            destination: destination,
            startedAt: startedAt,
            sync: sync ?? self.sync,
            sets: sets ?? self.sets,
            rest: rest ?? self.rest,
            currentMovementDataId: currentMovementDataId,
            prescriptions: prescriptions,
            addedMovementDataIds: addedMovementDataIds ?? self.addedMovementDataIds,
            notes: notes ?? self.notes,
            version: version)
    }
}

// MARK: - The document

extension WorkoutDestination: Codable {
    private enum CodingKeys: String, CodingKey {
        case kind
        case sessionId
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        switch try container.decode(String.self, forKey: .kind) {
        case "liveSession":
            self = .liveSession(try container.decode(UUID.self, forKey: .sessionId))
        case "afterTheFact":
            self = .afterTheFact
        case let kind:
            // Guessing would either orphan a session on the server or push a retro log into one
            // that is not there. Refusing means the document reads as absent, which is visible.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "'\(kind)' is not a workout destination."))
        }
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .liveSession(let id):
            try container.encode("liveSession", forKey: .kind)
            try container.encode(id, forKey: .sessionId)
        case .afterTheFact:
            try container.encode("afterTheFact", forKey: .kind)
        }
    }
}

extension WorkoutSyncState: Codable {
    private enum CodingKeys: String, CodingKey {
        case kind
        case detail
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        let detail = try container.decodeIfPresent(String.self, forKey: .detail) ?? ""
        switch try container.decode(String.self, forKey: .kind) {
        case "synced": self = .synced
        case "failed": self = .failed(detail)
        case "refused": self = .refused(detail)
        // Anything else — "pending", or a case a later build invents — becomes pending, which is
        // the safe reading: it says the sets may not be on the server, and the next push carries
        // them. There is no sync state where losing a set is the consequence of being wrong.
        default: self = .pending
        }
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        switch self {
        case .synced:
            try container.encode("synced", forKey: .kind)
        case .pending:
            try container.encode("pending", forKey: .kind)
        case .failed(let detail):
            try container.encode("failed", forKey: .kind)
            try container.encode(detail, forKey: .detail)
        case .refused(let detail):
            try container.encode("refused", forKey: .kind)
            try container.encode(detail, forKey: .detail)
        }
    }
}

extension ActiveWorkout: Codable {
    private enum CodingKeys: String, CodingKey {
        case version
        case destination
        case startedAt
        case sync
        case sets
        case rest
        case currentMovementDataId
        case prescriptions
        case addedMovementDataIds
        case notes
        /// Version 1's single session id, read so a workout started under the foundation build is
        /// not thrown away on the first launch of this one.
        case sessionId
    }

    /// Reads a document, tolerating everything whose absence costs nothing.
    ///
    /// The two members that are genuinely load-bearing — where the sets go, and the sets — are the
    /// only ones that can fail the read. A rest clock that no longer decodes is a forgotten
    /// countdown; a set list that silently became empty is lost work, so `sets` is decoded strictly
    /// and a failure propagates rather than defaulting to none.
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)

        version = try container.decodeIfPresent(Int.self, forKey: .version) ?? 1
        startedAt = try container.decode(Date.self, forKey: .startedAt)

        if let destination = try container.decodeIfPresent(WorkoutDestination.self, forKey: .destination) {
            self.destination = destination
        } else {
            // Version 1 held a bare session id and had no retro-logging path, so every document it
            // wrote is a live session.
            self.destination = .liveSession(try container.decode(UUID.self, forKey: .sessionId))
        }

        sets = try container.decodeIfPresent([LoggedSet].self, forKey: .sets) ?? []
        sync = (try? container.decode(WorkoutSyncState.self, forKey: .sync)) ?? .pending
        rest = (try? container.decode(RestClock.self, forKey: .rest)) ?? .idle
        let current = try? container.decode(UUID.self, forKey: .currentMovementDataId)
        let prescriptions = (try? container.decode([SetPrescription].self, forKey: .prescriptions)) ?? []
        currentMovementDataId = current
        self.prescriptions = prescriptions
        addedMovementDataIds = (try? container.decode([UUID].self, forKey: .addedMovementDataIds))
            ?? Self.addedBeforeTheListExisted(current: current, sets: sets, prescriptions: prescriptions)
        notes = (try? container.decode(String.self, forKey: .notes)) ?? ""
    }

    /// What a document written before version 3 had added, recovered from the one trace it kept.
    ///
    /// Version 2 held only the movement being worked on. One that no set and no target names is
    /// almost always a movement the athlete picked and had not logged on yet — the workout in
    /// flight on a phone that updates mid-session — and it is carried forward as added so it keeps
    /// a section to log against. It can also be a movement whose only set was deleted, since
    /// deleting a set leaves the current movement alone; that one comes back as an empty section
    /// the athlete can remove, which loses nothing.
    private static func addedBeforeTheListExisted(
        current: UUID?, sets: [LoggedSet], prescriptions: [SetPrescription]
    ) -> [UUID] {
        guard let current,
              !sets.contains(where: { $0.movementDataId == current }),
              !prescriptions.contains(where: { $0.movementDataId == current })
        else { return [] }
        return [current]
    }

    /// Written by hand because `sessionId` is a key with no property behind it — version 1's
    /// spelling, read but never written again — and a key without a property is what stops the
    /// compiler synthesizing this.
    ///
    /// `version` is always written as `currentVersion` rather than as the version read: a document
    /// this build rewrites has this build's shape, whatever shape it arrived in.
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(Self.currentVersion, forKey: .version)
        try container.encode(destination, forKey: .destination)
        try container.encode(startedAt, forKey: .startedAt)
        try container.encode(sync, forKey: .sync)
        try container.encode(sets, forKey: .sets)
        try container.encode(rest, forKey: .rest)
        try container.encodeIfPresent(currentMovementDataId, forKey: .currentMovementDataId)
        try container.encode(prescriptions, forKey: .prescriptions)
        try container.encode(addedMovementDataIds, forKey: .addedMovementDataIds)
        try container.encode(notes, forKey: .notes)
    }
}
