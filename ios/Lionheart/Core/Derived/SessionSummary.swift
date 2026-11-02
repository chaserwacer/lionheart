import Foundation

/// What a session says about itself in one line, without opening it.
///
/// It is a derivation rather than a view detail because the same reading appears on Today, in
/// History and in a program's list, and three screens counting sets three ways is how two numbers
/// start to disagree. The counts come from the session's own sets; volume, pace and PR status are
/// the server's answers and are **not** re-derived here.
struct SessionSummary: Equatable, Sendable {
    let day: CalendarDay
    let setCount: Int
    /// How many different movements the sets touched. The server deleted the movement wrapper
    /// entity, so this is a client-side reading of the flat set list — and it counts a movement
    /// returned to later once, not twice.
    let movementCount: Int
    /// Seconds, where the session recorded an interval. An instant has no duration.
    let duration: TimeInterval?
    let hasEffort: Bool
    let notes: String

    var hasNotes: Bool { !notes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
}

extension SessionSummary {
    static func of(_ session: TrainingSession.Completed) -> SessionSummary {
        SessionSummary(
            day: session.envelope.date,
            setCount: session.sets.count,
            movementCount: distinctMovements(in: session.sets),
            duration: session.period.duration,
            hasEffort: session.effort.ratings.map { !$0.isEmpty } ?? false,
            notes: session.envelope.notes)
    }

    /// How many different movements the sets touched.
    ///
    /// Sets with no movement identity — a trail run, a freeform log — count as none rather than as
    /// one shared nameless movement, which is why they are dropped before the set is taken.
    static func distinctMovements(in sets: [SetEntry]) -> Int {
        Set(sets.compactMap(\.movementDataId)).count
    }
}

/// What a planned session says about itself before it is done.
struct PlanSummary: Equatable, Sendable {
    let plannedFor: CalendarDay
    let prescriptionCount: Int
    let movementCount: Int
    let notes: String

    var hasNotes: Bool { !notes.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }

    static func of(_ session: TrainingSession.Planned) -> PlanSummary {
        PlanSummary(
            plannedFor: session.plannedFor,
            prescriptionCount: session.prescriptions.count,
            movementCount: Set(session.prescriptions.compactMap(\.movementDataId)).count,
            notes: session.envelope.notes)
    }
}

extension SetPrescription {
    /// The movement this prescription targets, where the shape names one.
    var movementDataId: UUID? {
        switch self {
        case .lift(let prescription): prescription.movementDataId
        case .distanceTime(let prescription): prescription.movementDataId
        case .freeform, .unrecognised: nil
        }
    }
}
