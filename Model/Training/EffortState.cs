namespace lionheart.Model.Training;

/// <summary>Perceived-effort fill-state: reported or not — never a nullable ratings object.</summary>
public abstract record EffortState;

public sealed record EffortNotReported : EffortState;

public sealed record EffortReported(PerceivedEffortRatings Ratings) : EffortState;
