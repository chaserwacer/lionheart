using Microsoft.EntityFrameworkCore;

/// <summary>
/// Representation of perceived effort metrics of something performed.
/// </summary>
[Owned]
public record PerceivedEffortRatings
{
    public int? AccumulatedFatigue { get; set; }
    public int? DifficultyRating { get; set; }
    public int? EngagementRating { get; set; }
    public int? ExternalVariablesRating { get; set; }
}

