namespace lionheart.ActivityTracking;

/// <summary>
/// Class to represent an activity a user completes. 
/// </summary>
public class Activity
{
    // Base Data
    public Guid ActivityID { get; init; }
    public Guid UserID { get; init; }
    public DateTime DateTime { get; init; }
    public string TimeSpan { get; init; } = string.Empty;
    public int CaloriesBurned { get; set; }
    public string Name { get; init; } = string.Empty;
    public string UserSummary { get; set; } = string.Empty;

    // 'Feel' Data
    public int AccumulatedFatigue { get; set; }
    public int DifficultyRating { get; set; }
    public int EngagementRating { get; set; }
    public int ExternalVariablesRating { get; set; }

    // Sport Specific Data
    public RunWalkDetails? RunWalkDetails { get; set; }
    public LiftDetails? LiftDetails { get; set; }
    public RideDetails? RideDetails { get; set; }
}

public class RunWalkDetails
{
    public Guid ActivityID { get; set; }
    public double Distance { get; set; }
    public int ElevationGain { get; set; }
    public string AveragePace { get; set; } = string.Empty;
    public List<string> MileSplits { get; set; } = [];
    public string RunType { get; set; } = string.Empty;  // Ex: Zone 2 Rail Trail, Exploration Hike, Road Walk

}
public class LiftDetails
{
    public Guid ActivityID { get; set; }
    public int Tonnage { get; set; }
    public string LiftType { get; set; } = string.Empty; // Ex: PL, BodyBuilding
    public string LiftFocus { get; set; } = string.Empty; // Ex: Legs, Squat + Bench, Shoulders & Arms

}

public class RideDetails
{
    public Guid ActivityID { get; set; }
    public double Distance { get; set; }
    public int ElevationGain { get; set; }
    public int AveragePower { get; set; }
    public double AverageSpeed { get; set; }
    public string RideType { get; set; } = string.Empty; // Ex: Mtb Trail Ride
}

