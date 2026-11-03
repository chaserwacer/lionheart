namespace lionheart.Model.Training.Programs;

/// <summary>
/// Plans a program. A program is always born planned — activating and completing it are
/// transitions, so neither the lifecycle state nor a summary is a client's to state here.
/// </summary>
public sealed record PlanProgramRequest(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> Tags);

/// <summary>
/// Active → Completed. The summary exists only on a completed program, which is why it arrives
/// with the transition rather than being editable throughout.
/// </summary>
public sealed record CompleteProgramRequest(string Summary);

/// <summary>
/// The editable description of a program — everything except its lifecycle state and its summary.
/// Every field is replaced, in any lifecycle state: renaming a block or moving its dates says
/// nothing about whether it has started.
/// </summary>
public sealed record UpdateProgramRequest(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> Tags);
