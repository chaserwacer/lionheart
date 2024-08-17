namespace lionheart.Model.Oura;
public class DailyOuraInfo
{
    public Guid ObjectID { get; init; }
    public Guid UserID {get; init;}
    public DateOnly Date { get; set; }
    public DateOnly SyncDate { get; set; }

    public required ResilienceData ResilienceData { get; set; }
    public required ActivityData ActivityData { get; set; }
    public required SleepData SleepData {get; set; }
    public required ReadinessData ReadinessData { get; set; }

    public string ActivityJson { get; set; } = string.Empty;
    public string ResilienceJson { get; set; } = string.Empty;
    public string SleepJson { get; set; } = string.Empty;
    public string ReadinessJson { get; set; } = string.Empty;
}

public class SleepData{
    public int SleepScore { get; set; }
    public int DeepSleep { get; set; }   
    public int Efficiency { get; set; }  
    public int Latency { get; set; }     
    public int RemSleep { get; set; }    
    public int Restfulness { get; set; } 
    public int Timing { get; set; }      
    public int TotalSleep { get; set; }  
}
public class ActivityData
{
    public int ActivityScore { get; set; }
    public int Steps { get; set; }
    public int ActiveCalories { get; set; }
    public int TotalCalories { get; set; }
    public int TargetCalories { get; set; }
    public int MeetDailyTargets { get; set; }
    public int MoveEveryHour { get; set; }
    public int RecoveryTime { get; set; }
    public int StayActive { get; set; }
    public int TrainingFrequency { get; set; }
    public int TrainingVolume { get; set; }
}
public class ResilienceData
{
    public int SleepRecovery { get; set; }
    public int DaytimeRecovery { get; set; }
    public int Stress { get; set; }
    public string ResilienceLevel { get; set; } = string.Empty;
}
public class ReadinessData
{
    public int ReadinessScore { get; set; }
    public int TemperatureDeviation { get; set; }
    public int ActivityBalance { get; set; }
    public int BodyTemperature { get; set; }
    public int HrvBalance { get; set; }
    public int PreviousDayActivity { get; set; }
    public int PreviousNight { get; set; }
    public int RecoveryIndex { get; set; }
    public int RestingHeartRate { get; set; }
    public int SleepBalance { get; set; }
}
