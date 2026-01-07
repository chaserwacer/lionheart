using Microsoft.EntityFrameworkCore;

/// <summary>
/// Representation of perceived effort metrics of something performed.
/// </summary>
[Owned]
public record PerceivedEffortRatings
{
    /// <summary>
    /// Timestamp when ratings were recorded. Serves as existence marker.
    /// </summary>
    public required DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public int? AccumulatedFatigue { get; set; }
    public int? DifficultyRating { get; set; }
    public int? EngagementRating { get; set; }
    public int? ExternalVariablesRating { get; set; }
}

