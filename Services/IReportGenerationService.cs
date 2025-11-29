using Ardalis.Result;
using lionheart.Model.Report;

namespace lionheart.Services;

/// <summary>
/// Service interface for AI-powered report content generation.
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// Generates the content for a report based on the provided context.
    /// </summary>
    Task<Result<GeneratedReportContent>> GenerateReportContentAsync(IReportContext context);
}

/// <summary>
/// The generated content for a report.
/// </summary>
public record GeneratedReportContent
{
    /// <summary>
    /// The main summary text of the report.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Key insights extracted from the data.
    /// </summary>
    public List<ReportInsight> Insights { get; init; } = [];

    /// <summary>
    /// Actionable recommendations.
    /// </summary>
    public List<ReportRecommendation> Recommendations { get; init; } = [];
}
