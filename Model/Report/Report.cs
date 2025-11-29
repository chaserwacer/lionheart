using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DKNet.EfCore.DtoGenerator;

namespace lionheart.Model.Report;

/// <summary>
/// Entity representing a generated report stored in the database.
/// Reports are dynamically generated based on user data and scheduled times.
/// </summary>
public class Report : IReport
{
    [Key]
    public Guid ReportID { get; init; }

    /// <summary>
    /// The user who owns this report.
    /// </summary>
    public Guid UserID { get; init; }

    /// <summary>
    /// The type of report.
    /// </summary>
    public ReportType Type { get; init; }

    /// <summary>
    /// The date this report is for.
    /// </summary>
    public DateOnly ReportDate { get; init; }

    /// <summary>
    /// When this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// Current status of the report.
    /// </summary>
    public ReportStatus Status { get; set; }

    /// <summary>
    /// The main AI-generated summary of the report.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Title of the report (auto-generated based on type and date).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Key insights extracted from the report.
    /// Stored as JSON for flexibility.
    /// </summary>
    public string InsightsJson { get; set; } = "[]";

    /// <summary>
    /// Actionable recommendations from the report.
    /// Stored as JSON for flexibility.
    /// </summary>
    public string RecommendationsJson { get; set; } = "[]";

    /// <summary>
    /// The context data used to generate this report.
    /// Stored as JSON for auditing/regeneration.
    /// </summary>
    public string ContextJson { get; set; } = "{}";

    /// <summary>
    /// For TrainingSession reports, the associated session ID.
    /// </summary>
    public Guid? TrainingSessionID { get; set; }

    /// <summary>
    /// Gets the report context from the stored JSON.
    /// </summary>
    public IReportContext GetContext()
    {
        return Type switch
        {
            ReportType.Morning => System.Text.Json.JsonSerializer.Deserialize<MorningReportContext>(ContextJson) 
                ?? new MorningReportContext(),
            ReportType.Evening => System.Text.Json.JsonSerializer.Deserialize<EveningReportContext>(ContextJson) 
                ?? new EveningReportContext(),
            ReportType.TrainingSession => System.Text.Json.JsonSerializer.Deserialize<TrainingSessionReportContext>(ContextJson) 
                ?? new TrainingSessionReportContext(),
            _ => throw new InvalidOperationException($"Unknown report type: {Type}")
        };
    }

    /// <summary>
    /// Generates an appropriate title based on report type and date.
    /// </summary>
    public static string GenerateTitle(ReportType type, DateOnly date)
    {
        return type switch
        {
            ReportType.Morning => $"Morning Readiness Report - {date:MMMM d, yyyy}",
            ReportType.Evening => $"Evening Recovery Report - {date:MMMM d, yyyy}",
            ReportType.TrainingSession => $"Training Session Analysis - {date:MMMM d, yyyy}",
            _ => $"Report - {date:MMMM d, yyyy}"
        };
    }

    // DTO generation using the existing pattern in the codebase
    [GenerateDto(typeof(Report),
                 Exclude = new[] { "ContextJson" })]
    public partial record ReportDTO;

    [GenerateDto(typeof(Report),
                 Include = new[] { "Type", "ReportDate", "TrainingSessionID" })]
    public partial record CreateReportRequest;

    [GenerateDto(typeof(Report),
                 Include = new[] { "ReportID", "Status", "Summary", "InsightsJson", "RecommendationsJson" })]
    public partial record UpdateReportRequest;
}

/// <summary>
/// Request to generate a morning report for a specific date.
/// </summary>
public record GenerateMorningReportRequest
{
    [Required]
    public DateOnly ReportDate { get; init; }
}

/// <summary>
/// Request to generate an evening report for a specific date.
/// </summary>
public record GenerateEveningReportRequest
{
    [Required]
    public DateOnly ReportDate { get; init; }
}

/// <summary>
/// Request to generate a training session report.
/// </summary>
public record GenerateTrainingSessionReportRequest
{
    [Required]
    public Guid TrainingSessionID { get; init; }
}

/// <summary>
/// DTO for returning a list of insights.
/// </summary>
public record ReportInsight
{
    public string Category { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public InsightSeverity Severity { get; init; }
}

/// <summary>
/// DTO for returning a recommendation.
/// </summary>
public record ReportRecommendation
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RecommendationPriority Priority { get; init; }
}

/// <summary>
/// Severity levels for report insights.
/// </summary>
public enum InsightSeverity
{
    Info,
    Positive,
    Warning,
    Critical
}

/// <summary>
/// Priority levels for recommendations.
/// </summary>
public enum RecommendationPriority
{
    Low,
    Medium,
    High
}
