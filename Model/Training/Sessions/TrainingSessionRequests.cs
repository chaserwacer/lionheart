using lionheart.Model.Analysis;

namespace lionheart.Model.Training.Sessions;

// Purpose-built commands, one per route (docs/api-and-composition.md §1). None of them carries a
// TrainingSessionId, a UserId, a Provenance or a Decoration: the server owns all four, and a
// client able to state them could author an AI-provenance session, claim another athlete's id, or
// inject a fabricated decoration into the projection analytics read.
//
// ProgramMembership crosses the wire as a plain optional program id rather than as its two-case
// fill-state, for the reason docs/api-surface.md gives for the two context creates: a flat shape
// generates a far better TypeScript client. The domain's Standalone/InProgram is restored at the
// service boundary, so nothing downstream sees the optionality. RoutineOrigin crosses the same way,
// as an optional routine id.

/// <summary>Plans a session for a future date. Prescriptions are targets; no set has happened yet.</summary>
public sealed record PlanSessionRequest(
    DateOnly PlannedFor,
    IReadOnlyList<SetPrescriptionCommand> Prescriptions,
    string Notes,
    Guid? TrainingProgramId);

/// <summary>Starts a session that was never planned — the athlete simply began training.</summary>
/// <remarks>
/// A <see cref="RoutineId"/> that is not one of the athlete's routines is dropped rather than
/// refused, here and on <see cref="LogCompletedSessionRequest"/>: a workout started offline from a
/// routine deleted meanwhile still happened, and must still land.
/// </remarks>
public sealed record StartUnplannedSessionRequest(
    DateTimeOffset StartedAt,
    string Notes,
    Guid? TrainingProgramId,
    Guid? RoutineId);

/// <summary>
/// Logs a session that already happened, in one call. The common case: the athlete writes it down
/// afterwards rather than driving the lifecycle live.
/// </summary>
public sealed record LogCompletedSessionRequest(
    IReadOnlyList<SetCommand> Sets,
    PeriodCommand Period,
    PerceivedEffortCommand? Effort,
    string Notes,
    Guid? TrainingProgramId,
    Guid? RoutineId);

/// <summary>Planned → Active.</summary>
public sealed record StartSessionRequest(DateTimeOffset StartedAt);

/// <summary>Active → Completed.</summary>
public sealed record CompleteSessionRequest(PeriodCommand Period, PerceivedEffortCommand? Effort);

/// <summary>
/// Completed → Completed: corrects a session after the fact. The sets, when it happened and the
/// notes are restated whole, for the reason <see cref="ReplaceSetsRequest"/> gives for sets; the
/// effort, the program and the routine stay as they were.
/// </summary>
public sealed record ReviseSessionRequest(
    IReadOnlyList<SetCommand> Sets,
    PeriodCommand Period,
    string Notes);

/// <summary>
/// Replaces a session's whole ordered set list. There is deliberately no per-set route: the sets
/// persist as one JSON column, so a whole-list write is what happens at the storage layer however
/// the route is shaped, and per-index routes would add index-drift hazards for nothing.
/// </summary>
public sealed record ReplaceSetsRequest(IReadOnlyList<SetCommand> Sets);

/// <summary>Replaces a planned session's whole ordered prescription list. See <see cref="ReplaceSetsRequest"/>.</summary>
public sealed record ReplacePrescriptionsRequest(IReadOnlyList<SetPrescriptionCommand> Prescriptions);

/// <summary>Notes are editable in every lifecycle state — they are commentary, not state.</summary>
public sealed record SessionNotesRequest(string Notes);
