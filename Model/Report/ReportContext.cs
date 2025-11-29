namespace lionheart.Model.Report;

/// <summary>
/// Interface representing context data used for AI-powered report generation.
/// Different report types will have different context implementations.
/// </summary>
public interface IReportContext
{
    /// <summary>
    /// The type of report this context is for.
    /// </summary>
    ReportType ReportType { get; }

    /// <summary>
    /// Converts the context to a prompt-friendly string for AI generation.
    /// </summary>
    string ToPromptContext();
}

/// <summary>
/// Context data for morning reports.
/// Contains sleep data and readiness information.
/// </summary>
public class MorningReportContext : IReportContext
{
    public ReportType ReportType => ReportType.Morning;

    /// <summary>
    /// Sleep score from the previous night (0-100).
    /// </summary>
    public int? SleepScore { get; set; }

    /// <summary>
    /// Readiness score for the day (0-100).
    /// </summary>
    public int? ReadinessScore { get; set; }

    /// <summary>
    /// HRV balance indicator.
    /// </summary>
    public int? HrvBalance { get; set; }

    /// <summary>
    /// Resting heart rate from the night.
    /// </summary>
    public int? RestingHeartRate { get; set; }

    /// <summary>
    /// Deep sleep percentage/score.
    /// </summary>
    public int? DeepSleep { get; set; }

    /// <summary>
    /// REM sleep percentage/score.
    /// </summary>
    public int? RemSleep { get; set; }

    /// <summary>
    /// Sleep efficiency score.
    /// </summary>
    public int? SleepEfficiency { get; set; }

    /// <summary>
    /// Any scheduled training for today.
    /// </summary>
    public string? PlannedTrainingToday { get; set; }

    /// <summary>
    /// Previous day's training summary.
    /// </summary>
    public string? PreviousDayTrainingSummary { get; set; }

    /// <summary>
    /// User's wellness state from yesterday.
    /// </summary>
    public double? YesterdayWellnessScore { get; set; }

    public string ToPromptContext()
    {
        var context = $"Morning Report Context:\n" +
            $"- Sleep Score: {SleepScore?.ToString() ?? "N/A"}\n" +
            $"- Readiness Score: {ReadinessScore?.ToString() ?? "N/A"}\n" +
            $"- HRV Balance: {HrvBalance?.ToString() ?? "N/A"}\n" +
            $"- Resting Heart Rate: {RestingHeartRate?.ToString() ?? "N/A"}\n" +
            $"- Deep Sleep: {DeepSleep?.ToString() ?? "N/A"}\n" +
            $"- REM Sleep: {RemSleep?.ToString() ?? "N/A"}\n" +
            $"- Sleep Efficiency: {SleepEfficiency?.ToString() ?? "N/A"}\n" +
            $"- Yesterday's Wellness Score: {YesterdayWellnessScore?.ToString("F1") ?? "N/A"}\n";

        if (!string.IsNullOrEmpty(PlannedTrainingToday))
            context += $"- Planned Training Today: {PlannedTrainingToday}\n";

        if (!string.IsNullOrEmpty(PreviousDayTrainingSummary))
            context += $"- Previous Day Training: {PreviousDayTrainingSummary}\n";

        return context;
    }
}

/// <summary>
/// Context data for evening reports.
/// Contains activity data and recovery information.
/// </summary>
public class EveningReportContext : IReportContext
{
    public ReportType ReportType => ReportType.Evening;

    /// <summary>
    /// Activity score for the day (0-100).
    /// </summary>
    public int? ActivityScore { get; set; }

    /// <summary>
    /// Total steps for the day.
    /// </summary>
    public int? TotalSteps { get; set; }

    /// <summary>
    /// Active calories burned.
    /// </summary>
    public int? ActiveCalories { get; set; }

    /// <summary>
    /// Summary of training completed today.
    /// </summary>
    public string? TrainingCompletedToday { get; set; }

    /// <summary>
    /// Was the training session a PR or particularly good?
    /// </summary>
    public bool HadGoodTrainingSession { get; set; }

    /// <summary>
    /// User's self-reported wellness score for today.
    /// </summary>
    public double? TodayWellnessScore { get; set; }

    /// <summary>
    /// User's energy level.
    /// </summary>
    public int? EnergyScore { get; set; }

    /// <summary>
    /// User's stress level.
    /// </summary>
    public int? StressScore { get; set; }

    /// <summary>
    /// Planned training for tomorrow.
    /// </summary>
    public string? PlannedTrainingTomorrow { get; set; }

    public string ToPromptContext()
    {
        var context = $"Evening Report Context:\n" +
            $"- Activity Score: {ActivityScore?.ToString() ?? "N/A"}\n" +
            $"- Total Steps: {TotalSteps?.ToString() ?? "N/A"}\n" +
            $"- Active Calories: {ActiveCalories?.ToString() ?? "N/A"}\n" +
            $"- Today's Wellness Score: {TodayWellnessScore?.ToString("F1") ?? "N/A"}\n" +
            $"- Energy Level: {EnergyScore?.ToString() ?? "N/A"}\n" +
            $"- Stress Level: {StressScore?.ToString() ?? "N/A"}\n" +
            $"- Had Good Training Session: {HadGoodTrainingSession}\n";

        if (!string.IsNullOrEmpty(TrainingCompletedToday))
            context += $"- Training Completed: {TrainingCompletedToday}\n";

        if (!string.IsNullOrEmpty(PlannedTrainingTomorrow))
            context += $"- Planned Training Tomorrow: {PlannedTrainingTomorrow}\n";

        return context;
    }
}

/// <summary>
/// Context data for training session reports.
/// Contains detailed session data and performance metrics.
/// </summary>
public class TrainingSessionReportContext : IReportContext
{
    public ReportType ReportType => ReportType.TrainingSession;

    /// <summary>
    /// ID of the training session being analyzed.
    /// </summary>
    public Guid TrainingSessionID { get; set; }

    /// <summary>
    /// Overall session quality (good/bad/neutral).
    /// </summary>
    public SessionQuality SessionQuality { get; set; }

    /// <summary>
    /// List of movements performed with their performance notes.
    /// </summary>
    public List<MovementPerformance> Movements { get; set; } = [];

    /// <summary>
    /// List of any PRs achieved in this session.
    /// </summary>
    public List<PersonalRecord> PersonalRecords { get; set; } = [];

    /// <summary>
    /// Sleep score from the night before.
    /// </summary>
    public int? PreSessionSleepScore { get; set; }

    /// <summary>
    /// Readiness score on the day of training.
    /// </summary>
    public int? PreSessionReadinessScore { get; set; }

    /// <summary>
    /// Wellness score on the day of training.
    /// </summary>
    public double? PreSessionWellnessScore { get; set; }

    /// <summary>
    /// Notes from the user about the session.
    /// </summary>
    public string? SessionNotes { get; set; }

    /// <summary>
    /// How many days since last training session.
    /// </summary>
    public int DaysSinceLastSession { get; set; }

    /// <summary>
    /// Any injuries the user currently has.
    /// </summary>
    public List<string> CurrentInjuries { get; set; } = [];

    public string ToPromptContext()
    {
        var context = $"Training Session Report Context:\n" +
            $"- Session Quality: {SessionQuality}\n" +
            $"- Pre-Session Sleep Score: {PreSessionSleepScore?.ToString() ?? "N/A"}\n" +
            $"- Pre-Session Readiness Score: {PreSessionReadinessScore?.ToString() ?? "N/A"}\n" +
            $"- Pre-Session Wellness Score: {PreSessionWellnessScore?.ToString("F1") ?? "N/A"}\n" +
            $"- Days Since Last Session: {DaysSinceLastSession}\n";

        if (PersonalRecords.Count > 0)
        {
            context += "- Personal Records Achieved:\n";
            foreach (var pr in PersonalRecords)
                context += $"  * {pr.MovementName}: {pr.Description}\n";
        }

        if (Movements.Count > 0)
        {
            context += "- Movements Performed:\n";
            foreach (var movement in Movements)
                context += $"  * {movement.Name}: {movement.SetsCompleted} sets, " +
                    $"Performance: {movement.PerformanceRating}/10\n";
        }

        if (CurrentInjuries.Count > 0)
        {
            context += $"- Current Injuries: {string.Join(", ", CurrentInjuries)}\n";
        }

        if (!string.IsNullOrEmpty(SessionNotes))
            context += $"- Session Notes: {SessionNotes}\n";

        return context;
    }
}

/// <summary>
/// Represents a movement's performance in a training session.
/// </summary>
public class MovementPerformance
{
    public string Name { get; set; } = string.Empty;
    public int SetsCompleted { get; set; }
    public int SetsPlanned { get; set; }
    public int PerformanceRating { get; set; } // 1-10
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Represents a personal record achieved in a training session.
/// </summary>
public class PersonalRecord
{
    public string MovementName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; // e.g., "New 1RM: 315 lbs"
    public double PreviousBest { get; set; }
    public double NewRecord { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>
/// Enum representing the overall quality of a training session.
/// </summary>
public enum SessionQuality
{
    Poor,
    BelowAverage,
    Average,
    AboveAverage,
    Excellent
}
