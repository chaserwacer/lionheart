namespace lionheart.Model.Oura
{
    /// <summary>
    /// Object containing a panel of Oura Ring data for a user for a given date.
    /// </summary>
    /// <remarks>
    /// This class is a subset of the information retrevied from Oura Ring API [modified for lionheart use].
    /// </remarks>
    public class DailyOuraData
    {
        public Guid ObjectID { get; init; }
        public Guid UserID {get; init;}
        public DateOnly Date { get; set; }
        public DateOnly SyncDate { get; set; }

        public required ResilienceData ResilienceData { get; set; }
        public required ActivityData ActivityData { get; set; }
        public required SleepData SleepData {get; set; }
        public required ReadinessData ReadinessData { get; set; }

        /// <summary>
        /// Raw JSON data from Oura API for activity data.
        /// </summary>
        public string ActivityJson { get; set; } = string.Empty;
        /// <summary>
        /// Raw JSON data from Oura API for resilience data.
        /// </summary>
        public string ResilienceJson { get; set; } = string.Empty;
        /// <summary>
        /// Raw JSON data from Oura API for sleep data.
        /// </summary>
        public string SleepJson { get; set; } = string.Empty;
        /// <summary>
        /// Raw JSON data from Oura API for readiness data.
        /// </summary>
        public string ReadinessJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// Panel of sleep data retrieved from Oura Ring API for a given day.
    /// </summary>
    /// <remarks>
    /// This class is a subset of the information retrevied from Oura Ring API [modified for lionheart use].
    /// </remarks>
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
    
    /// <summary>
    /// Panel of activity data retrieved from Oura Ring API for a given day.
    /// </summary>
    /// <remarks>
    /// This class is a subset of the information retrevied from Oura Ring API [modified for lionheart use].
    /// </remarks>
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
    /// <summary>
    /// Panel of resilience data retrieved from Oura Ring API for a given day.
    /// </summary>
    /// <remarks>
    /// This class is a subset of the information retrevied from Oura Ring API [modified for lionheart use].
    /// </remarks>
    public class ResilienceData
    {
        public double SleepRecovery { get; set; }
        public double DaytimeRecovery { get; set; }
        public double Stress { get; set; }
        public string ResilienceLevel { get; set; } = string.Empty;
    }
    /// <summary>
    /// Panel of readiness data retrieved from Oura Ring API for a given day.
    /// </summary>
    /// <remarks>
    /// This class is a subset of the information retrevied from Oura Ring API [modified for lionheart use].
    /// </remarks>
    public class ReadinessData
    {
        public int ReadinessScore { get; set; }
        public double TemperatureDeviation { get; set; }
        public int ActivityBalance { get; set; }
        public int BodyTemperature { get; set; }
        public int HrvBalance { get; set; }
        public int PreviousDayActivity { get; set; }
        public int PreviousNight { get; set; }
        public int RecoveryIndex { get; set; }
        public int RestingHeartRate { get; set; }
        public int SleepBalance { get; set; }
    }
}
