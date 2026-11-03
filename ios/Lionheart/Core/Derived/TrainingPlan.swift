import Foundation

/// What the Train tab can offer the athlete right now, read from one window of sessions.
///
/// One read rather than three, for the reason the Today screen reads one window: separate requests
/// for "what is in progress", "what is planned" and "what did I miss" can disagree with each other,
/// and the tab's whole job is to be unambiguous about what to do next.
///
/// The day rules are the ones `docs/ios-architecture.md` settles for Today, applied here so the two
/// screens cannot say different things about the same session: a planned session dated **before**
/// today has been missed rather than scheduled, and it stays visible because starting it late is
/// normal.
struct TrainingPlan: Equatable, Sendable {
    /// The session being trained, if the server holds one. The most recently started, because the
    /// server permits more than one active session per athlete and the device owns only one
    /// workout — see `ActiveWorkoutStore`.
    let inProgress: TrainingSession.Active?
    /// Planned for today.
    let today: [TrainingSession.Planned]
    /// Planned for a later day, soonest first.
    let upcoming: [TrainingSession.Planned]
    /// Planned for a day gone by and never started or skipped, most recent first.
    let missed: [TrainingSession.Planned]

    var hasNothingPlanned: Bool { today.isEmpty && upcoming.isEmpty && missed.isEmpty }

    static func reading(sessions: [TrainingSession], today day: CalendarDay) -> TrainingPlan {
        let planned = sessions.compactMap(\.plannedSession)

        return TrainingPlan(
            inProgress: sessions.compactMap(\.activeSession)
                .max { $0.startedAt < $1.startedAt },
            today: planned.filter { $0.plannedFor == day },
            upcoming: planned.filter { $0.plannedFor > day }
                .sorted { $0.plannedFor < $1.plannedFor },
            missed: planned.filter { $0.plannedFor < day }
                .sorted { $0.plannedFor > $1.plannedFor })
    }

    /// The plan without the sessions in `held`: nothing on it offers to start or resume training
    /// the device is already holding.
    func excluding(_ held: Set<UUID>) -> TrainingPlan {
        guard !held.isEmpty else { return self }
        let isFree = { (session: TrainingSession.Planned) in !held.contains(session.envelope.trainingSessionId) }
        return TrainingPlan(
            inProgress: inProgress.flatMap { held.contains($0.envelope.trainingSessionId) ? nil : $0 },
            today: today.filter(isFree),
            upcoming: upcoming.filter(isFree),
            missed: missed.filter(isFree))
    }

    /// Every session id the device's workouts stand for on the server: the session each is logged
    /// against, and the plan each was started from.
    static func sessionIds(heldBy workouts: [ActiveWorkout]) -> Set<UUID> {
        Set(workouts.flatMap { [$0.destination.sessionId, $0.plannedSessionId].compactMap { $0 } })
    }
}

extension TrainingSession {
    /// This session if it is planned. A named accessor rather than a `case` test at each site,
    /// because the Train tab asks three times in one derivation.
    var plannedSession: Planned? {
        switch self {
        case .planned(let session): session
        case .active, .completed, .skipped, .unrecognised: nil
        }
    }

    var activeSession: Active? {
        switch self {
        case .active(let session): session
        case .planned, .completed, .skipped, .unrecognised: nil
        }
    }
}
