using System.Diagnostics;
using System.Text.Json.Serialization;

namespace lionheart.Model.Training.Programs;

/// <summary>
/// A program on the wire. A program carries no analysis internals, so unlike the session and
/// context views this one hides only <c>UserId</c> — it exists for the other half of the job:
/// <see cref="TrainingProgram"/> is a relational-spine entity with no JSON discriminators, so
/// without this envelope the lifecycle hierarchy would flatten to its base on the way out and the
/// generated TypeScript client would not know the three states apart.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(PlannedProgramView), "planned")]
[JsonDerivedType(typeof(ActiveProgramView), "active")]
[JsonDerivedType(typeof(CompletedProgramView), "completed")]
public abstract record TrainingProgramView
{
    private protected TrainingProgramView(TrainingProgram program)
    {
        TrainingProgramId = program.TrainingProgramId;
        Title = program.Title;
        StartDate = program.StartDate;
        EndDate = program.EndDate;
        Tags = program.Tags;
    }

    public Guid TrainingProgramId { get; init; }
    public string Title { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public IReadOnlyList<string> Tags { get; init; }

    public static TrainingProgramView Of(TrainingProgram program) => program switch
    {
        PlannedTrainingProgram planned => new PlannedProgramView(planned),
        ActiveTrainingProgram active => new ActiveProgramView(active),
        CompletedTrainingProgram completed => new CompletedProgramView(completed),
        _ => throw new UnreachableException()
    };
}

public sealed record PlannedProgramView : TrainingProgramView
{
    public PlannedProgramView(PlannedTrainingProgram program) : base(program) { }
}

public sealed record ActiveProgramView : TrainingProgramView
{
    public ActiveProgramView(ActiveTrainingProgram program) : base(program) { }
}

public sealed record CompletedProgramView : TrainingProgramView
{
    public CompletedProgramView(CompletedTrainingProgram program) : base(program) =>
        Summary = program.Summary;

    public string Summary { get; init; }
}
