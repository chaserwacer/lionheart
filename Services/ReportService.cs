using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Report;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace lionheart.Services;

/// <summary>
/// Service for managing and generating reports.
/// </summary>
[McpServerToolType]
public class ReportService : IReportService
{
    private readonly ModelContext _context;
    private readonly IReportGenerationService _reportGenerationService;
    private readonly IOuraService _ouraService;
    private readonly IWellnessService _wellnessService;
    private readonly ITrainingSessionService _trainingSessionService;
    private readonly IInjuryService _injuryService;

    public ReportService(
        ModelContext context,
        IReportGenerationService reportGenerationService,
        IOuraService ouraService,
        IWellnessService wellnessService,
        ITrainingSessionService trainingSessionService,
        IInjuryService injuryService)
    {
        _context = context;
        _reportGenerationService = reportGenerationService;
        _ouraService = ouraService;
        _wellnessService = wellnessService;
        _trainingSessionService = trainingSessionService;
        _injuryService = injuryService;
    }

    [McpServerTool, Description("Get all reports for a user within a date range.")]
    public async Task<Result<List<Report.ReportDTO>>> GetReportsAsync(
        IdentityUser user,
        DateOnly startDate,
        DateOnly endDate)
    {
        var userGuid = Guid.Parse(user.Id);

        var reports = await _context.Reports
            .Where(r => r.UserID == userGuid && 
                        r.ReportDate >= startDate && 
                        r.ReportDate <= endDate)
            .OrderByDescending(r => r.GeneratedAt)
            .ToListAsync();

        return Result<List<Report.ReportDTO>>.Success(
            reports.Select(r => r.ToReportDTO()).ToList());
    }

    [McpServerTool, Description("Get reports of a specific type.")]
    public async Task<Result<List<Report.ReportDTO>>> GetReportsByTypeAsync(
        IdentityUser user,
        ReportType type,
        int limit = 10)
    {
        var userGuid = Guid.Parse(user.Id);

        var reports = await _context.Reports
            .Where(r => r.UserID == userGuid && r.Type == type)
            .OrderByDescending(r => r.GeneratedAt)
            .Take(limit)
            .ToListAsync();

        return Result<List<Report.ReportDTO>>.Success(
            reports.Select(r => r.ToReportDTO()).ToList());
    }

    [McpServerTool, Description("Get a specific report by ID.")]
    public async Task<Result<Report.ReportDTO>> GetReportAsync(
        IdentityUser user,
        Guid reportId)
    {
        var userGuid = Guid.Parse(user.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.ReportID == reportId && r.UserID == userGuid);

        if (report is null)
            return Result<Report.ReportDTO>.NotFound("Report not found.");

        return Result<Report.ReportDTO>.Success(report.ToReportDTO());
    }

    [McpServerTool, Description("Generate a morning report analyzing sleep and readiness for the day.")]
    public async Task<Result<Report.ReportDTO>> GenerateMorningReportAsync(
        IdentityUser user,
        GenerateMorningReportRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Check if a morning report already exists for this date
        var existingReport = await _context.Reports
            .FirstOrDefaultAsync(r => r.UserID == userGuid && 
                                      r.ReportDate == request.ReportDate && 
                                      r.Type == ReportType.Morning);

        if (existingReport is not null)
        {
            return Result<Report.ReportDTO>.Error(
                $"A morning report already exists for {request.ReportDate}. Use regenerate to update it.");
        }

        // Build the context for the morning report
        var context = await BuildMorningContextAsync(user, request.ReportDate);

        // Create the report entity
        var report = new Report
        {
            ReportID = Guid.NewGuid(),
            UserID = userGuid,
            Type = ReportType.Morning,
            ReportDate = request.ReportDate,
            GeneratedAt = DateTime.UtcNow,
            Status = ReportStatus.Generating,
            Title = Report.GenerateTitle(ReportType.Morning, request.ReportDate),
            ContextJson = JsonSerializer.Serialize(context)
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Generate the report content using AI
        var generationResult = await _reportGenerationService.GenerateReportContentAsync(context);

        if (!generationResult.IsSuccess)
        {
            report.Status = ReportStatus.Failed;
            await _context.SaveChangesAsync();
            return Result<Report.ReportDTO>.Error(generationResult.Errors.ToArray());
        }

        // Update the report with generated content
        report.Summary = generationResult.Value.Summary;
        report.InsightsJson = JsonSerializer.Serialize(generationResult.Value.Insights);
        report.RecommendationsJson = JsonSerializer.Serialize(generationResult.Value.Recommendations);
        report.Status = ReportStatus.Generated;
        
        await _context.SaveChangesAsync();

        return Result<Report.ReportDTO>.Success(report.ToReportDTO());
    }

    [McpServerTool, Description("Generate an evening report reflecting on the day's activities.")]
    public async Task<Result<Report.ReportDTO>> GenerateEveningReportAsync(
        IdentityUser user,
        GenerateEveningReportRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Check if an evening report already exists for this date
        var existingReport = await _context.Reports
            .FirstOrDefaultAsync(r => r.UserID == userGuid && 
                                      r.ReportDate == request.ReportDate && 
                                      r.Type == ReportType.Evening);

        if (existingReport is not null)
        {
            return Result<Report.ReportDTO>.Error(
                $"An evening report already exists for {request.ReportDate}. Use regenerate to update it.");
        }

        // Build the context for the evening report
        var context = await BuildEveningContextAsync(user, request.ReportDate);

        // Create the report entity
        var report = new Report
        {
            ReportID = Guid.NewGuid(),
            UserID = userGuid,
            Type = ReportType.Evening,
            ReportDate = request.ReportDate,
            GeneratedAt = DateTime.UtcNow,
            Status = ReportStatus.Generating,
            Title = Report.GenerateTitle(ReportType.Evening, request.ReportDate),
            ContextJson = JsonSerializer.Serialize(context)
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Generate the report content using AI
        var generationResult = await _reportGenerationService.GenerateReportContentAsync(context);

        if (!generationResult.IsSuccess)
        {
            report.Status = ReportStatus.Failed;
            await _context.SaveChangesAsync();
            return Result<Report.ReportDTO>.Error(generationResult.Errors.ToArray());
        }

        // Update the report with generated content
        report.Summary = generationResult.Value.Summary;
        report.InsightsJson = JsonSerializer.Serialize(generationResult.Value.Insights);
        report.RecommendationsJson = JsonSerializer.Serialize(generationResult.Value.Recommendations);
        report.Status = ReportStatus.Generated;
        
        await _context.SaveChangesAsync();

        return Result<Report.ReportDTO>.Success(report.ToReportDTO());
    }

    [McpServerTool, Description("Generate a training session report analyzing performance and PRs.")]
    public async Task<Result<Report.ReportDTO>> GenerateTrainingSessionReportAsync(
        IdentityUser user,
        GenerateTrainingSessionReportRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Verify the training session exists and belongs to the user
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID &&
                                       ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
            return Result<Report.ReportDTO>.NotFound("Training session not found or access denied.");

        // Check if a report already exists for this session
        var existingReport = await _context.Reports
            .FirstOrDefaultAsync(r => r.UserID == userGuid && 
                                      r.TrainingSessionID == request.TrainingSessionID);

        if (existingReport is not null)
        {
            return Result<Report.ReportDTO>.Error(
                "A report already exists for this training session. Use regenerate to update it.");
        }

        // Build the context for the training session report
        var context = await BuildTrainingSessionContextAsync(user, session);

        // Create the report entity
        var report = new Report
        {
            ReportID = Guid.NewGuid(),
            UserID = userGuid,
            Type = ReportType.TrainingSession,
            ReportDate = session.Date,
            TrainingSessionID = session.TrainingSessionID,
            GeneratedAt = DateTime.UtcNow,
            Status = ReportStatus.Generating,
            Title = Report.GenerateTitle(ReportType.TrainingSession, session.Date),
            ContextJson = JsonSerializer.Serialize(context)
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Generate the report content using AI
        var generationResult = await _reportGenerationService.GenerateReportContentAsync(context);

        if (!generationResult.IsSuccess)
        {
            report.Status = ReportStatus.Failed;
            await _context.SaveChangesAsync();
            return Result<Report.ReportDTO>.Error(generationResult.Errors.ToArray());
        }

        // Update the report with generated content
        report.Summary = generationResult.Value.Summary;
        report.InsightsJson = JsonSerializer.Serialize(generationResult.Value.Insights);
        report.RecommendationsJson = JsonSerializer.Serialize(generationResult.Value.Recommendations);
        report.Status = ReportStatus.Generated;
        
        await _context.SaveChangesAsync();

        return Result<Report.ReportDTO>.Success(report.ToReportDTO());
    }

    [McpServerTool, Description("Regenerate an existing report with fresh data.")]
    public async Task<Result<Report.ReportDTO>> RegenerateReportAsync(
        IdentityUser user,
        Guid reportId)
    {
        var userGuid = Guid.Parse(user.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.ReportID == reportId && r.UserID == userGuid);

        if (report is null)
            return Result<Report.ReportDTO>.NotFound("Report not found.");

        // Rebuild context based on report type
        IReportContext context = report.Type switch
        {
            ReportType.Morning => await BuildMorningContextAsync(user, report.ReportDate),
            ReportType.Evening => await BuildEveningContextAsync(user, report.ReportDate),
            ReportType.TrainingSession => await RebuildTrainingSessionContextAsync(user, report),
            _ => throw new InvalidOperationException($"Unknown report type: {report.Type}")
        };

        report.Status = ReportStatus.Generating;
        report.ContextJson = JsonSerializer.Serialize(context);
        await _context.SaveChangesAsync();

        // Regenerate content
        var generationResult = await _reportGenerationService.GenerateReportContentAsync(context);

        if (!generationResult.IsSuccess)
        {
            report.Status = ReportStatus.Failed;
            await _context.SaveChangesAsync();
            return Result<Report.ReportDTO>.Error(generationResult.Errors.ToArray());
        }

        report.Summary = generationResult.Value.Summary;
        report.InsightsJson = JsonSerializer.Serialize(generationResult.Value.Insights);
        report.RecommendationsJson = JsonSerializer.Serialize(generationResult.Value.Recommendations);
        report.Status = ReportStatus.Generated;
        report.GeneratedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        return Result<Report.ReportDTO>.Success(report.ToReportDTO());
    }

    [McpServerTool, Description("Delete a report.")]
    public async Task<Result> DeleteReportAsync(IdentityUser user, Guid reportId)
    {
        var userGuid = Guid.Parse(user.Id);

        var report = await _context.Reports
            .FirstOrDefaultAsync(r => r.ReportID == reportId && r.UserID == userGuid);

        if (report is null)
            return Result.NotFound("Report not found.");

        _context.Reports.Remove(report);
        await _context.SaveChangesAsync();

        return Result.NoContent();
    }

    [McpServerTool, Description("Get the latest report of each type for dashboard display.")]
    public async Task<Result<Dictionary<ReportType, Report.ReportDTO>>> GetLatestReportsAsync(
        IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);

        var latestReports = new Dictionary<ReportType, Report.ReportDTO>();

        foreach (ReportType type in Enum.GetValues<ReportType>())
        {
            var latestReport = await _context.Reports
                .Where(r => r.UserID == userGuid && r.Type == type && r.Status == ReportStatus.Generated)
                .OrderByDescending(r => r.GeneratedAt)
                .FirstOrDefaultAsync();

            if (latestReport is not null)
            {
                latestReports[type] = latestReport.ToReportDTO();
            }
        }

        return Result<Dictionary<ReportType, Report.ReportDTO>>.Success(latestReports);
    }

    #region Context Building Methods

    private async Task<MorningReportContext> BuildMorningContextAsync(IdentityUser user, DateOnly reportDate)
    {
        var context = new MorningReportContext();

        // Get Oura data for the report date
        var ouraResult = await _ouraService.GetDailyOuraInfoAsync(user, reportDate);
        if (ouraResult.IsSuccess && ouraResult.Value is not null)
        {
            var ouraData = ouraResult.Value;
            context.SleepScore = ouraData.SleepData?.Score;
            context.ReadinessScore = ouraData.ReadinessData?.Score;
            context.HrvBalance = ouraData.ReadinessData?.Contributors?.HrvBalance;
            context.RestingHeartRate = ouraData.ReadinessData?.Contributors?.RestingHeartRate;
            context.DeepSleep = ouraData.SleepData?.Contributors?.DeepSleep;
            context.RemSleep = ouraData.SleepData?.Contributors?.RemSleep;
            context.SleepEfficiency = ouraData.SleepData?.Contributors?.Efficiency;
        }

        // Get yesterday's wellness
        var yesterdayDate = reportDate.AddDays(-1);
        var wellnessResult = await _wellnessService.GetWellnessAsync(user, yesterdayDate);
        if (wellnessResult.IsSuccess && wellnessResult.Value is not null)
        {
            context.YesterdayWellnessScore = wellnessResult.Value.OverallScore;
        }

        // Get today's planned training
        var dateRange = new Model.DTOs.DateRangeRequest 
        { 
            StartDate = reportDate, 
            EndDate = reportDate 
        };
        var sessionsResult = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, dateRange);
        if (sessionsResult.IsSuccess && sessionsResult.Value.Count > 0)
        {
            var sessions = sessionsResult.Value;
            context.PlannedTrainingToday = $"{sessions.Count} session(s) planned";
        }

        // Get yesterday's training summary
        var yesterdayRange = new Model.DTOs.DateRangeRequest 
        { 
            StartDate = yesterdayDate, 
            EndDate = yesterdayDate 
        };
        var yesterdaySessionsResult = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, yesterdayRange);
        if (yesterdaySessionsResult.IsSuccess && yesterdaySessionsResult.Value.Count > 0)
        {
            var completed = yesterdaySessionsResult.Value.Count(s => s.Status == TrainingSessionStatus.Completed);
            context.PreviousDayTrainingSummary = $"{completed} session(s) completed";
        }

        return context;
    }

    private async Task<EveningReportContext> BuildEveningContextAsync(IdentityUser user, DateOnly reportDate)
    {
        var context = new EveningReportContext();

        // Get Oura activity data
        var ouraResult = await _ouraService.GetDailyOuraInfoAsync(user, reportDate);
        if (ouraResult.IsSuccess && ouraResult.Value is not null)
        {
            var ouraData = ouraResult.Value;
            context.ActivityScore = ouraData.ActivityData?.Score;
            context.TotalSteps = ouraData.ActivityData?.Steps;
            context.ActiveCalories = ouraData.ActivityData?.ActiveCalories;
        }

        // Get today's wellness
        var wellnessResult = await _wellnessService.GetWellnessAsync(user, reportDate);
        if (wellnessResult.IsSuccess && wellnessResult.Value is not null)
        {
            context.TodayWellnessScore = wellnessResult.Value.OverallScore;
            context.EnergyScore = wellnessResult.Value.EnergyScore;
            context.StressScore = wellnessResult.Value.StressScore;
        }

        // Get today's completed training
        var dateRange = new Model.DTOs.DateRangeRequest 
        { 
            StartDate = reportDate, 
            EndDate = reportDate 
        };
        var sessionsResult = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, dateRange);
        if (sessionsResult.IsSuccess && sessionsResult.Value.Count > 0)
        {
            var completed = sessionsResult.Value
                .Where(s => s.Status == TrainingSessionStatus.Completed)
                .ToList();
            
            if (completed.Count > 0)
            {
                var totalMovements = completed.Sum(s => s.Movements.Count);
                context.TrainingCompletedToday = $"{completed.Count} session(s), {totalMovements} total movements";
                context.HadGoodTrainingSession = true; // This could be more sophisticated
            }
        }

        // Get tomorrow's planned training
        var tomorrowDate = reportDate.AddDays(1);
        var tomorrowRange = new Model.DTOs.DateRangeRequest 
        { 
            StartDate = tomorrowDate, 
            EndDate = tomorrowDate 
        };
        var tomorrowSessionsResult = await _trainingSessionService.GetTrainingSessionsByDateRangeAsync(user, tomorrowRange);
        if (tomorrowSessionsResult.IsSuccess && tomorrowSessionsResult.Value.Count > 0)
        {
            context.PlannedTrainingTomorrow = $"{tomorrowSessionsResult.Value.Count} session(s) planned";
        }

        return context;
    }

    private async Task<TrainingSessionReportContext> BuildTrainingSessionContextAsync(
        IdentityUser user, 
        TrainingSession session)
    {
        var context = new TrainingSessionReportContext
        {
            TrainingSessionID = session.TrainingSessionID,
            SessionNotes = session.Notes
        };

        // Determine session quality based on completion
        context.SessionQuality = DetermineSessionQuality(session);

        // Get pre-session data (previous night's sleep, day's readiness)
        var ouraResult = await _ouraService.GetDailyOuraInfoAsync(user, session.Date);
        if (ouraResult.IsSuccess && ouraResult.Value is not null)
        {
            var ouraData = ouraResult.Value;
            context.PreSessionSleepScore = ouraData.SleepData?.Score;
            context.PreSessionReadinessScore = ouraData.ReadinessData?.Score;
        }

        // Get wellness score
        var wellnessResult = await _wellnessService.GetWellnessAsync(user, session.Date);
        if (wellnessResult.IsSuccess && wellnessResult.Value is not null)
        {
            context.PreSessionWellnessScore = wellnessResult.Value.OverallScore;
        }

        // Build movement performance data
        foreach (var movement in session.Movements)
        {
            var completedSets = movement.Sets.Count(s => s.ActualReps.HasValue);
            var plannedSets = movement.Sets.Count;
            
            context.Movements.Add(new MovementPerformance
            {
                Name = movement.MovementBase?.Name ?? "Unknown",
                SetsCompleted = completedSets,
                SetsPlanned = plannedSets,
                PerformanceRating = CalculatePerformanceRating(movement),
                Notes = movement.Notes ?? string.Empty
            });

            // Check for PRs (simplified - would need historical data for real PR detection)
            var bestSet = movement.Sets
                .Where(s => s.ActualWeight.HasValue && s.ActualReps.HasValue)
                .OrderByDescending(s => s.ActualWeight!.Value * s.ActualReps!.Value)
                .FirstOrDefault();

            if (bestSet is not null && bestSet.ActualWeight > bestSet.RecommendedWeight * 1.1)
            {
                context.PersonalRecords.Add(new PersonalRecord
                {
                    MovementName = movement.MovementBase?.Name ?? "Unknown",
                    Description = $"{bestSet.ActualWeight} for {bestSet.ActualReps} reps",
                    NewRecord = bestSet.ActualWeight ?? 0,
                    PreviousBest = bestSet.RecommendedWeight ?? 0,
                    Unit = "lbs"
                });
            }
        }

        // Get days since last session
        var userGuid = Guid.Parse(user.Id);
        var previousSession = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgram!.UserID == userGuid && 
                        ts.Date < session.Date)
            .OrderByDescending(ts => ts.Date)
            .FirstOrDefaultAsync();

        if (previousSession is not null)
        {
            context.DaysSinceLastSession = session.Date.DayNumber - previousSession.Date.DayNumber;
        }

        // Get current injuries
        var injuriesResult = await _injuryService.GetActiveInjuriesAsync(user);
        if (injuriesResult.IsSuccess && injuriesResult.Value.Count > 0)
        {
            context.CurrentInjuries = injuriesResult.Value
                .Select(i => $"{i.BodyPart}: {i.Description}")
                .ToList();
        }

        return context;
    }

    private async Task<TrainingSessionReportContext> RebuildTrainingSessionContextAsync(
        IdentityUser user,
        Report report)
    {
        if (!report.TrainingSessionID.HasValue)
            throw new InvalidOperationException("Training session report missing session ID.");

        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == report.TrainingSessionID &&
                                       ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
            throw new InvalidOperationException("Training session not found.");

        return await BuildTrainingSessionContextAsync(user, session);
    }

    private static SessionQuality DetermineSessionQuality(TrainingSession session)
    {
        if (session.Status != TrainingSessionStatus.Completed)
            return SessionQuality.Average;

        var movements = session.Movements;
        if (movements.Count == 0)
            return SessionQuality.Average;

        var completedMovements = movements.Count(m => m.IsCompleted);
        var completionRate = (double)completedMovements / movements.Count;

        // Calculate average RPE vs recommended
        var setsWithActual = movements
            .SelectMany(m => m.Sets)
            .Where(s => s.ActualRPE.HasValue && s.RecommendedRPE.HasValue)
            .ToList();

        if (setsWithActual.Count > 0)
        {
            var avgRpeDiff = setsWithActual.Average(s => s.ActualRPE!.Value - s.RecommendedRPE!.Value);
            
            if (completionRate >= 1.0 && avgRpeDiff <= -1)
                return SessionQuality.Excellent;
            if (completionRate >= 0.9 && avgRpeDiff <= 0)
                return SessionQuality.AboveAverage;
            if (completionRate >= 0.7)
                return SessionQuality.Average;
            if (completionRate >= 0.5)
                return SessionQuality.BelowAverage;
            return SessionQuality.Poor;
        }

        return completionRate switch
        {
            >= 0.9 => SessionQuality.AboveAverage,
            >= 0.7 => SessionQuality.Average,
            >= 0.5 => SessionQuality.BelowAverage,
            _ => SessionQuality.Poor
        };
    }

    private static int CalculatePerformanceRating(Movement movement)
    {
        if (movement.Sets.Count == 0)
            return 5;

        var setsWithActual = movement.Sets
            .Where(s => s.ActualReps.HasValue && s.ActualWeight.HasValue)
            .ToList();

        if (setsWithActual.Count == 0)
            return 5;

        // Compare actual vs recommended performance
        var totalScore = 0;
        foreach (var set in setsWithActual)
        {
            var repsScore = set.RecommendedReps > 0 
                ? (double)set.ActualReps!.Value / set.RecommendedReps * 5 
                : 5;
            var weightScore = set.RecommendedWeight > 0 
                ? (double)set.ActualWeight!.Value / set.RecommendedWeight!.Value * 5 
                : 5;
            totalScore += (int)Math.Round((repsScore + weightScore) / 2);
        }

        return Math.Clamp(totalScore / setsWithActual.Count, 1, 10);
    }

    #endregion
}

/// <summary>
/// Extension methods for Report entity.
/// </summary>
public static class ReportExtensions
{
    public static Report.ReportDTO ToReportDTO(this Report report)
    {
        return new Report.ReportDTO
        {
            ReportID = report.ReportID,
            UserID = report.UserID,
            Type = report.Type,
            ReportDate = report.ReportDate,
            GeneratedAt = report.GeneratedAt,
            Status = report.Status,
            Summary = report.Summary,
            Title = report.Title,
            InsightsJson = report.InsightsJson,
            RecommendationsJson = report.RecommendationsJson,
            TrainingSessionID = report.TrainingSessionID
        };
    }
}
