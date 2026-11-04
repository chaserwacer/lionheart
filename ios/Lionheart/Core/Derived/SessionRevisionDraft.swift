import Foundation

/// A completed session being corrected: its sets, when it happened and its notes, as the athlete
/// is restating them before `POST /{id}/revise` carries them whole.
///
/// Each set holds an id of the device's own for the life of the draft — the server keeps no per-set
/// identity, and editing the third of five by position would drift the moment one was removed.
/// A movement picked with nothing logged on it yet is held apart, as the workout in flight holds
/// one, so it has a section to log its first set in; it joins the sets with that set.
struct SessionRevisionDraft: Equatable, Sendable {
    /// One set in the draft, addressed by `id` rather than by where it sits.
    struct RevisedSet: Equatable, Sendable, Identifiable {
        let id: UUID
        let command: SetCommand
    }

    /// A movement and the consecutive run of sets on it — the session's own order, so a return to
    /// the bench after squatting is two blocks, as `SessionDetail` reads it.
    struct Block: Equatable, Sendable, Identifiable {
        let id: Int
        /// `nil` for the sets no movement names: trail runs and freeform work.
        let movementDataId: UUID?
        let sets: [RevisedSet]
    }

    /// Why a session cannot be corrected here. Writing a session back whole means stating every
    /// set, so a set or a period this build cannot state would be lost by a save.
    enum Refusal: Error, Equatable, Sendable {
        case notCompleted
        case unreadableSet
        case unreadablePeriod

        var message: String {
            switch self {
            case .notCompleted: "Only a completed session can be edited."
            case .unreadableSet: "This session holds a set this version of Tempo cannot write back, so editing it here would lose that set."
            case .unreadablePeriod: "This session's time could not be read, so it cannot be written back."
            }
        }
    }

    let sessionId: UUID
    private(set) var sets: [RevisedSet]
    /// Movements picked with no set logged on them yet, in the order picked.
    private(set) var addedMovements: [UUID]
    private(set) var start: Date
    /// When it finished, or `nil` for a session logged as one moment.
    private(set) var end: Date?
    var notes: String

    private let original: Original

    /// What the session was when the draft opened, for whether anything has changed and whether
    /// the change reaches the records.
    private struct Original: Equatable, Sendable {
        let sets: [SetCommand]
        let start: Date
        let end: Date?
        let notes: String
    }

    /// Opens a draft of `session`, or says why it cannot be corrected here.
    static func of(_ session: TrainingSession, makeId: () -> UUID = UUID.init) -> Result<SessionRevisionDraft, Refusal> {
        guard case .completed(let completed) = session else { return .failure(.notCompleted) }
        let commands = completed.sets.compactMap(\.command)
        guard commands.count == completed.sets.count else { return .failure(.unreadableSet) }
        let span: (Date, Date?)
        switch completed.period {
        case .instant(let at): span = (at, nil)
        case .interval(let start, let end): span = (start, end)
        case .unrecognised: return .failure(.unreadablePeriod)
        }
        return .success(SessionRevisionDraft(
            sessionId: completed.envelope.trainingSessionId,
            sets: commands.map { RevisedSet(id: makeId(), command: $0) },
            addedMovements: [],
            start: span.0,
            end: span.1,
            notes: completed.envelope.notes,
            original: Original(sets: commands, start: span.0, end: span.1, notes: completed.envelope.notes)))
    }

    // MARK: - Reading

    /// The sets as blocks, then each movement picked and not yet logged on, as an empty block.
    var blocks: [Block] {
        let runs = sets.reduce(into: [(UUID?, [RevisedSet])]()) { runs, set in
            if let last = runs.last, last.0 == set.command.movementDataId {
                runs[runs.count - 1].1.append(set)
            } else {
                runs.append((set.command.movementDataId, [set]))
            }
        }
        let logged = runs.enumerated().map { Block(id: $0.offset, movementDataId: $0.element.0, sets: $0.element.1) }
        let waiting = addedMovements.enumerated().map {
            Block(id: logged.count + $0.offset, movementDataId: $0.element, sets: [])
        }
        return logged + waiting
    }

    var commands: [SetCommand] { sets.map(\.command) }

    var hasChanges: Bool {
        commands != original.sets || start != original.start || end != original.end
            || notes != original.notes
    }

    /// Whether saving will have the server re-detect the records — the server's own rule: the sets
    /// changed, or the start moved. A start within the second it was is the same start, since the
    /// instant travels at millisecond precision.
    var reachesRecords: Bool {
        commands != original.sets || abs(start.timeIntervalSince(original.start)) >= 1
    }

    /// Why the draft cannot be saved as it stands, or `nil` when it can.
    var problem: String? {
        if sets.isEmpty { return "A session needs at least one set." }
        if let end, end < start { return "It cannot finish before it started." }
        return nil
    }

    var isSavable: Bool { hasChanges && problem == nil }

    /// The fields a new set on `block` starts from: its last set's numbers, or — on a movement with
    /// none yet — an empty lift in the weight unit the session already uses.
    func newSetDraft(for block: Block) -> SetDraft {
        if let last = block.sets.last { return SetDraft(from: last.command) }
        guard let movementDataId = block.movementDataId else { return SetDraft(from: .freeform(text: "")) }
        var draft = SetDraft(from: .lift(SetCommand.Lift(
            movementDataId: movementDataId, reps: 0, weight: 0, unit: weightUnit ?? .kilograms, rpe: .unrated)))
        // A nought here is a placeholder rather than a weight lifted; see `SetDraft(next:in:)`.
        draft.weight = ""
        return draft
    }

    /// The weight unit the session's lifts are in.
    private var weightUnit: WeightUnit? {
        sets.lazy.compactMap { set -> WeightUnit? in
            if case .lift(let lift) = set.command { return lift.unit }
            return nil
        }.first
    }

    // MARK: - Changing

    mutating func replace(_ id: UUID, with command: SetCommand) {
        sets = sets.map { $0.id == id ? RevisedSet(id: id, command: command) : $0 }
    }

    mutating func remove(_ id: UUID) {
        sets.removeAll { $0.id == id }
    }

    /// Adds a set to the end of `block`: after its last set, or — for a movement with none yet —
    /// at the end of the session, which is where a movement picked afterwards was done.
    mutating func append(_ command: SetCommand, to block: Block, makeId: () -> UUID = UUID.init) {
        let added = RevisedSet(id: makeId(), command: command)
        if let last = block.sets.last, let index = sets.firstIndex(where: { $0.id == last.id }) {
            sets.insert(added, at: index + 1)
        } else {
            sets.append(added)
            if let movement = block.movementDataId { addedMovements.removeAll { $0 == movement } }
        }
    }

    /// Picks a movement to log on. One already waiting is not picked twice.
    mutating func addMovement(_ movementDataId: UUID) {
        guard !addedMovements.contains(movementDataId) else { return }
        addedMovements.append(movementDataId)
    }

    /// Drops a movement picked with nothing logged on it.
    mutating func dropMovement(_ movementDataId: UUID) {
        addedMovements.removeAll { $0 == movementDataId }
    }

    /// Moves the session to start at `start`, keeping how long it lasted — a day corrected is the
    /// same session on another day, not a longer one.
    mutating func moveStart(to start: Date) {
        let duration = end.map { $0.timeIntervalSince(self.start) }
        self.start = start
        if let duration { end = start.addingTimeInterval(duration) }
    }

    /// Sets when it finished. Only a session logged with a length has one to set.
    mutating func setEnd(_ end: Date) {
        guard self.end != nil else { return }
        self.end = end
    }
}
