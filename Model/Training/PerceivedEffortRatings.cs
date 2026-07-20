using Microsoft.EntityFrameworkCore;

/// <summary>
/// Representation of perceived effort metrics of something performed.
/// </summary>
[Owned]
public record PerceivedEffortRatings
{
    private readonly DateTime _recordedAt = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when ratings were recorded. Serves as existence marker.
    /// </summary>
    /// <remarks>
    /// Bookkeeping rather than training time, so it is stored as plain UTC — unlike a
    /// <see cref="Analysis.Period"/>, no one asks which calendar day it landed on. An
    /// Unspecified input is read as local, matching how a bare DateTime converts everywhere else.
    /// </remarks>
    public required DateTime RecordedAt
    {
        get => _recordedAt;
        init => _recordedAt = value.ToUniversalTime();
    }
    public int? AccumulatedFatigue { get; set; }
    public int? DifficultyRating { get; set; }
    public int? EngagementRating { get; set; }
    public int? ExternalVariablesRating { get; set; }
}

