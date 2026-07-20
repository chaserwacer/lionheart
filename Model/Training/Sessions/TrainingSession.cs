using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

// Alias must live inside the namespace while the legacy lionheart.Model.Training.SetEntry
// namespace still exists — as an enclosing-namespace sibling it otherwise shadows the new type.
using SetEntry = lionheart.Model.Training.Sets.SetEntry;

/// <summary>Who authored the session content. Orthogonal to lifecycle — an AI-adjusted plan can still be completed.</summary>
public enum Provenance { User, AiAssisted }

/// <summary>Program membership as fill-state: standalone or belonging to a program. Never a nullable FK in the domain.</summary>
public abstract record ProgramMembership;
public sealed record Standalone : ProgramMembership;

public sealed record InProgram : ProgramMembership
{
    public InProgram(Guid trainingProgramId)
    {
        // Guid.Empty is the storage sentinel for Standalone; letting it in here would
        // silently rehydrate as the wrong membership state.
        if (trainingProgramId == Guid.Empty)
            throw new ArgumentException("InProgram requires a real program id; use Standalone.", nameof(trainingProgramId));
        TrainingProgramId = trainingProgramId;
    }

    public Guid TrainingProgramId { get; }
}

/// <summary>
/// A training session's lifecycle as sealed subtypes — prescriptions exist only while
/// planned, actuals only once active/completed, decoration only on completion. Replaces
/// the old status enum (and its provenance-smuggling AIModified member).
/// </summary>
public abstract record TrainingSession
{
    public required Guid TrainingSessionId { get; init; }
    public required Guid UserId { get; init; }
    public required string Notes { get; init; }
    public required Provenance Provenance { get; init; }
    public required ProgramMembership Membership { get; init; }
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Derived from each state's own time-truth (planned-for date, start time, or performed
    /// period) — never stored separately, so it can't disagree with the state it came from.
    /// </summary>
    public abstract DateOnly Date { get; }
}

public sealed record PlannedTrainingSession(
    DateOnly PlannedFor,
    IReadOnlyList<SetPrescription> Prescriptions) : TrainingSession
{
    public override DateOnly Date => PlannedFor;
}

public sealed record ActiveTrainingSession(
    DateTime StartedAt,
    IReadOnlyList<SetEntry> Sets) : TrainingSession
{
    public override DateOnly Date => DateOnly.FromDateTime(StartedAt);
}

public sealed record CompletedTrainingSession(
    IReadOnlyList<SetEntry> Sets,
    Period Period,
    EffortState Effort,
    DecorationState Decoration) : TrainingSession, IAnalyzable
{
    public override DateOnly Date => DateOnly.FromDateTime(Period.StartTime);
}

public sealed record SkippedTrainingSession(DateOnly PlannedFor) : TrainingSession
{
    public override DateOnly Date => PlannedFor;
}
