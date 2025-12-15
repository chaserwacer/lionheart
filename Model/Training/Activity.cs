using System.ComponentModel.DataAnnotations;
using lionheart.Model.Training;

namespace lionheart.ActivityTracking;
public class Activity
{
    // Base Data
    public Guid ActivityID { get; init; }
    public Guid UserID { get; init; }
    public DateTime DateTime { get; set; }
    public int TimeInMinutes { get; set; }
    public int CaloriesBurned { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserSummary { get; set; } = string.Empty;
    public PerceivedEffortRatings? PerceivedEffortRatings { get; set; }
    public ActivityDetails? ActivityDetails { get; set; }
}
public class ActivityDetails
{
    public double? Distance { get; set; }
    public int? ElevationGain { get; set; }
    public int? AveragePower { get; set; }
    public double? AverageSpeed { get; set; }
    public string? Type { get; set; } = string.Empty; // Ex: Mtb Trail Ride
}

public record ActivityDTO(
    Guid ActivityID,
    Guid UserID,
    DateTime DateTime,
    int TimeInMinutes,
    int CaloriesBurned,
    string Name,
    string UserSummary,
    PerceivedEffortRatings? PerceivedEffortRatings,
    ActivityDetails? ActivityDetails
);
public record CreateActivityRequest(
    Guid UserID,
    DateTime DateTime,
    int TimeInMinutes,
    int CaloriesBurned,
    string Name,
    string UserSummary,
    PerceivedEffortRatings? PerceivedEffortRatings,
    ActivityDetails? ActivityDetails
);
public record UpdateActivityRequest(
    Guid ActivityID,
    Guid UserID,
    DateTime DateTime,
    int TimeInMinutes,
    int CaloriesBurned,
    string Name,
    string UserSummary,
    PerceivedEffortRatings? PerceivedEffortRatings,
    ActivityDetails? ActivityDetails
);
