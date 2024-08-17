namespace lionheart.Model.Oura.Dto;
public record OuraDailyActivityDocument
{
    public string Id { get; init; } = string.Empty;
    //public string Class5Min { get; init; } = string.Empty;
    public int? Score { get; init; }
    public int ActiveCalories { get; init; }
    //public int AverageMetMinutes { get; init; }
    public required ActivityContributorsDto Contributors { get; init; }
    //public int EquivalentWalkingDistance { get; init; }
    //public int HighActivityMetMinutes { get; init; }
    //public int HighActivityTime { get; init; }
    //public int InactivityAlerts { get; init; }
    //public int LowActivityMetMinutes { get; init; }
    //public int LowActivityTime { get; init; }
    //public int MediumActivityMetMinutes { get; init; }
    //public int MediumActivityTime { get; init; }
    //public required Met Met { get; init; } 
    //public int MetersToTarget { get; init; }
    //public int NonWearTime { get; init; }
    //public int RestingTime { get; init; }
    //public int SedentaryMetMinutes { get; init; }
    //public int SedentaryTime { get; init; }
    public int Steps { get; init; }
    public int TargetCalories { get; init; }
    //public int TargetMeters { get; init; }
    public int TotalCalories { get; init; }
    public DateOnly Day { get; init; }
    public string Timestamp { get; init; } = string.Empty;
}

public record ActivityContributorsDto
{
    public int? MeetDailyTargets { get; init; }
    public int? MoveEveryHour { get; init; }
    public int? RecoveryTime { get; init; }
    public int? StayActive { get; init; }
    public int? TrainingFrequency { get; init; }
    public int? TrainingVolume { get; init; }
}

// public record Met
// {
//     public int Interval { get; init; }
//     public List<object> Items { get; init; } = [];
//     public string Timestamp { get; init; }  = string.Empty;
// }



