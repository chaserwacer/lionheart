using Ardalis.Result;
using lionheart.Model.Report;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services;

/// <summary>
/// Service interface for managing reports.
/// Handles CRUD operations and report generation.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Gets all reports for a user within a date range.
    /// </summary>
    Task<Result<List<Report.ReportDTO>>> GetReportsAsync(
        IdentityUser user, 
        DateOnly startDate, 
        DateOnly endDate);

    /// <summary>
    /// Gets all reports of a specific type for a user.
    /// </summary>
    Task<Result<List<Report.ReportDTO>>> GetReportsByTypeAsync(
        IdentityUser user, 
        ReportType type, 
        int limit = 10);

    /// <summary>
    /// Gets a specific report by ID.
    /// </summary>
    Task<Result<Report.ReportDTO>> GetReportAsync(
        IdentityUser user, 
        Guid reportId);

    /// <summary>
    /// Generates a new morning report for the specified date.
    /// Collects sleep data, readiness scores, and scheduled training to provide
    /// insights on how the day might go.
    /// </summary>
    Task<Result<Report.ReportDTO>> GenerateMorningReportAsync(
        IdentityUser user, 
        GenerateMorningReportRequest request);

    /// <summary>
    /// Generates a new evening report for the specified date.
    /// Reflects on the day's activities, training completed, and recovery needs.
    /// </summary>
    Task<Result<Report.ReportDTO>> GenerateEveningReportAsync(
        IdentityUser user, 
        GenerateEveningReportRequest request);

    /// <summary>
    /// Generates a training session report.
    /// Analyzes the session performance, identifies PRs, and provides insights
    /// on what factors may have contributed to good or bad performance.
    /// </summary>
    Task<Result<Report.ReportDTO>> GenerateTrainingSessionReportAsync(
        IdentityUser user, 
        GenerateTrainingSessionReportRequest request);

    /// <summary>
    /// Regenerates an existing report with fresh data.
    /// </summary>
    Task<Result<Report.ReportDTO>> RegenerateReportAsync(
        IdentityUser user, 
        Guid reportId);

    /// <summary>
    /// Deletes a report.
    /// </summary>
    Task<Result> DeleteReportAsync(
        IdentityUser user, 
        Guid reportId);

    /// <summary>
    /// Gets the latest report of each type for a user (for dashboard display).
    /// </summary>
    Task<Result<Dictionary<ReportType, Report.ReportDTO>>> GetLatestReportsAsync(
        IdentityUser user);
}
