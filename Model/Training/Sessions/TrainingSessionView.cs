using System.Diagnostics;
using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// A session on the wire, carrying the same sealed lifecycle shape the domain has — which is the
/// whole payoff of having built the ADT, since it is what makes the generated TypeScript mirror it.
/// </summary>
/// <remarks>
/// Every nested shape is the domain's own (<see cref="SetEntry"/>, <see cref="SetPrescription"/>,
/// <see cref="Period"/>, <see cref="EffortState"/>), so nothing but the lifecycle envelope is
/// restated. The envelope is restated for two reasons the domain records cannot answer themselves:
/// <see cref="TrainingSession"/> carries no polymorphic JSON discriminators — it is a
/// relational-spine entity, so serializing one would flatten the hierarchy and the OpenAPI document
/// would describe only the base — and a completed session's decoration has to lose
/// <see cref="Decoration.SummaryText"/> and <see cref="Decoration.DecoratorVersion"/> on the way
/// out (see <see cref="DecorationView"/>). <c>UserId</c> goes with them: the caller is the owner on
/// every route, so it tells a client nothing it did not already send.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PlannedSessionView), "planned")]
[JsonDerivedType(typeof(ActiveSessionView), "active")]
[JsonDerivedType(typeof(CompletedSessionView), "completed")]
[JsonDerivedType(typeof(SkippedSessionView), "skipped")]
public abstract record TrainingSessionView
{
    /// <summary>
    /// Taking the domain record is what keeps the envelope filled in one place: a case cannot be
    /// added that forgets a member, and no case repeats the mapping.
    /// </summary>
    private protected TrainingSessionView(TrainingSession session)
    {
        TrainingSessionId = session.TrainingSessionId;
        Notes = session.Notes;
        Provenance = session.Provenance;
        TrainingProgramId = session.Membership is InProgram inProgram ? inProgram.TrainingProgramId : null;
        RoutineId = session.Origin is RoutineOrigin.FromRoutine fromRoutine ? fromRoutine.RoutineId : null;
        Source = session.Source;
        CreatedAt = session.CreatedAt;
        Date = session.Date;
    }

    public Guid TrainingSessionId { get; init; }
    public string Notes { get; init; }
    public Provenance Provenance { get; init; }

    /// <summary>The program this session belongs to, or absent when it stands alone.</summary>
    public Guid? TrainingProgramId { get; init; }

    /// <summary>
    /// The routine this session was trained from, or absent when it was ad hoc. The routine may
    /// have been deleted since; the session still says where it came from.
    /// </summary>
    public Guid? RoutineId { get; init; }

    /// <summary>
    /// Where the session came from: the athlete's own log, or a workout another app recorded and
    /// Apple Health holds — which has a route and the app's heart rate beside it.
    /// </summary>
    public SessionSource Source { get; init; }

    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// The calendar day the session belongs to, derived by each lifecycle state from its own
    /// time-truth. Sent rather than left to the client, because re-deriving it there means
    /// re-deriving the captured-offset rule <see cref="Period.LocalDate"/> exists to get right.
    /// </summary>
    public DateOnly Date { get; init; }

    public static TrainingSessionView Of(TrainingSession session) => session switch
    {
        PlannedTrainingSession planned => new PlannedSessionView(planned),
        ActiveTrainingSession active => new ActiveSessionView(active),
        CompletedTrainingSession completed => new CompletedSessionView(completed),
        SkippedTrainingSession skipped => new SkippedSessionView(skipped),
        _ => throw new UnreachableException()
    };
}

public sealed record PlannedSessionView : TrainingSessionView
{
    public PlannedSessionView(PlannedTrainingSession session) : base(session)
    {
        PlannedFor = session.PlannedFor;
        Prescriptions = session.Prescriptions;
    }

    public DateOnly PlannedFor { get; init; }
    public IReadOnlyList<SetPrescription> Prescriptions { get; init; }
}

public sealed record ActiveSessionView : TrainingSessionView
{
    public ActiveSessionView(ActiveTrainingSession session) : base(session)
    {
        StartedAt = session.StartedAt;
        Sets = session.Sets;
    }

    public DateTimeOffset StartedAt { get; init; }
    public IReadOnlyList<SetEntry> Sets { get; init; }
}

public sealed record CompletedSessionView : TrainingSessionView
{
    public CompletedSessionView(CompletedTrainingSession session) : base(session)
    {
        Sets = session.Sets;
        Period = session.Period;
        Effort = session.Effort;
        Decoration = DecorationView.Of(session.Decoration);
    }

    public IReadOnlyList<SetEntry> Sets { get; init; }
    public Period Period { get; init; }
    public EffortState Effort { get; init; }
    public DecorationView Decoration { get; init; }
}

public sealed record SkippedSessionView : TrainingSessionView
{
    public SkippedSessionView(SkippedTrainingSession session) : base(session) =>
        PlannedFor = session.PlannedFor;

    public DateOnly PlannedFor { get; init; }
}
