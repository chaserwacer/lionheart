namespace lionheart.Model.Report;

/// <summary>
/// Generic interface representing any type of report in the system.
/// Reports are dynamically generated based on user data and can be of different types
/// (Morning, Evening, TrainingSession).
/// </summary>
public interface IReport
{
    /// <summary>
    /// Unique identifier for the report.
    /// </summary>
    Guid ReportID { get; }

    /// <summary>
    /// The user who owns this report.
    /// </summary>
    Guid UserID { get; }

    /// <summary>
    /// The type of report (Morning, Evening, TrainingSession).
    /// </summary>
    ReportType Type { get; }

    /// <summary>
    /// The date this report is for.
    /// </summary>
    DateOnly ReportDate { get; }

    /// <summary>
    /// When this report was generated.
    /// </summary>
    DateTime GeneratedAt { get; }

    /// <summary>
    /// Current status of the report.
    /// </summary>
    ReportStatus Status { get; }

    /// <summary>
    /// The main content/summary of the report.
    /// </summary>
    string Summary { get; }

    /// <summary>
    /// Gets the report-specific context data for AI generation.
    /// </summary>
    IReportContext GetContext();
}

/// <summary>
/// Enum representing the type of report.
/// </summary>
public enum ReportType
{
    /// <summary>
    /// Morning report focusing on sleep quality and readiness for the day ahead.
    /// </summary>
    Morning,

    /// <summary>
    /// Evening report reflecting on the day's activities and recovery needs.
    /// </summary>
    Evening,

    /// <summary>
    /// Training session report analyzing performance, PRs, and factors affecting the session.
    /// </summary>
    TrainingSession
}

/// <summary>
/// Enum representing the status of a report.
/// </summary>
public enum ReportStatus
{
    /// <summary>
    /// Report is scheduled but not yet generated.
    /// </summary>
    Pending,

    /// <summary>
    /// Report is currently being generated.
    /// </summary>
    Generating,

    /// <summary>
    /// Report has been successfully generated.
    /// </summary>
    Generated,

    /// <summary>
    /// Report generation failed.
    /// </summary>
    Failed
}
