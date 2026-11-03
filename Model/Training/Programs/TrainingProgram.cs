namespace lionheart.Model.Training.Programs;

/// <summary>
/// A training program's lifecycle as sealed subtypes. Only a completed program can
/// carry a summary.
/// </summary>
public abstract record TrainingProgram
{
    public required Guid TrainingProgramId { get; init; }
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required IReadOnlyList<string> Tags { get; init; }
}

public sealed record PlannedTrainingProgram : TrainingProgram;

public sealed record ActiveTrainingProgram : TrainingProgram;

public sealed record CompletedTrainingProgram(string Summary) : TrainingProgram;
