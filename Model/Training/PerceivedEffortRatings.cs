using Microsoft.EntityFrameworkCore;

[Owned]
public record PerceivedEffortRatings
{

    public int? AccumulatedFatigue { get; set; }
    public int? DifficultyRating { get; set; }
    public int? EngagementRating { get; set; }
    public int? ExternalVariablesRating { get; set; }
}

