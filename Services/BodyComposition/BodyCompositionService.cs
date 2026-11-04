using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.BodyComposition;

public interface IBodyCompositionService
{
    Task<Result<ScanView, Problem>> RecordAsync(IdentityUser user, ScanRequest request, CancellationToken ct);

    Task<Result<ScanView, Problem>> RestateAsync(IdentityUser user, Guid scanId, ScanRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<ScanView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct);

    Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid scanId, CancellationToken ct);
}

/// <summary>
/// The athlete's body-composition scans, and the bodyweight each one measured.
/// </summary>
/// <remarks>
/// A scan weighs the athlete, so recording one is also a bodyweight measurement for its day,
/// written through the same staging the bodyweight route uses and in the same save. The bodyweight
/// series therefore stays the one answer to "what did he weigh that day" — the system load of a
/// weighted dip reads it unchanged — and deleting a scan leaves the measurement, which was real.
/// Every write marks the chat card's Recent State stale, since it states the latest scan.
/// </remarks>
[ToolProvider]
public sealed class BodyCompositionService : IBodyCompositionService
{
    private readonly ModelContext _context;

    public BodyCompositionService(ModelContext context) => _context = context;

    public Task<Result<ScanView, Problem>> RecordAsync(IdentityUser user, ScanRequest request, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => Stated(request).Map(stated => (userId, stated)))
            .BindAsync(asked => RecordedAsync(asked.userId, asked.stated, ct));

    public Task<Result<ScanView, Problem>> RestateAsync(
        IdentityUser user, Guid scanId, ScanRequest request, CancellationToken ct) =>
        Stated(request)
            .BindAsync(stated => OwnedAsync(Guid.Parse(user.Id), scanId, ct).MapAsync(scan => (scan, stated)))
            .BindAsync(found => RestatedAsync(found.scan, found.stated, ct));

    public async Task<Result<IReadOnlyList<ScanView>, Problem>> GetAllAsync(IdentityUser user, CancellationToken ct) =>
        Result<IReadOnlyList<ScanView>, Problem>.Success(await AllAsync(Guid.Parse(user.Id), ct));

    public Task<Result<Unit, Problem>> DeleteAsync(IdentityUser user, Guid scanId, CancellationToken ct) =>
        OwnedAsync(Guid.Parse(user.Id), scanId, ct).MapAsync(scan => RemovedAsync(scan, ct));

    [Tool(Name = "GetBodyCompositionScans",
        Description = "Every body-composition scan the athlete has recorded (InBody result sheets), oldest first, one block of text per scan with every figure written with its unit: the day and time, the analyser model, height, weight, skeletal muscle mass, body fat mass and percent, lean body mass and BMI; then the panels that analyser printed — body water, dry lean mass, segmental lean and segmental fat (each arm, the trunk and each leg, with the percentage of the analyser's own reference for the athlete's height and weight, not a clinical norm), ECW/TBW, the analyser's fitness score, its suggested fat and lean change, basal metabolic rate, visceral fat level, and segmental impedance by frequency.")]
    public async Task<IReadOnlyList<string>> GetBodyCompositionScansForChatAsync(IdentityUser user) =>
        [.. (await ScansAsync(Guid.Parse(user.Id), CancellationToken.None)).Select(BodyFacts.Sheet)];

    // ---- Guard ----

    /// <summary>The scan as stated, with the bodyweight its analyser measured.</summary>
    private static Result<StatedScan, Problem> Stated(ScanRequest request) =>
        SheetRules.Checked(request.Sheet)
            .Bind(sheet => Bodyweight.Of(sheet.Weight, sheet.Unit)
                .Map(bodyweight => new StatedScan(request.Day, request.Time, sheet, bodyweight, request.RecordsBodyweight)));

    private async Task<Result<BodyCompositionScan, Problem>> OwnedAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var scan = await _context.BodyCompositionScans
            .FirstOrDefaultAsync(s => s.ScanId == scanId && s.UserId == userId, ct);

        return scan is null
            ? Result<BodyCompositionScan, Problem>.Failure(new Problem.NotFound("No such body-composition scan."))
            : Result<BodyCompositionScan, Problem>.Success(scan);
    }

    // ---- Stage and commit ----

    private async Task<Result<ScanView, Problem>> RecordedAsync(Guid userId, StatedScan stated, CancellationToken ct)
    {
        var scan = BodyCompositionScan.Taken(Guid.CreateVersion7(), userId, stated.Day, stated.Time, stated.Sheet);
        _context.BodyCompositionScans.Add(scan);
        await StageBodyweightAsync(userId, stated, ct);
        await _context.StageRecentStateInvalidationAsync(userId, ct);

        return await SavedAsync(scan, ct);
    }

    /// <remarks>
    /// A corrected day moves the bodyweight the scan wrote: a measurement on the old day that is the
    /// scan's own weight was the scan's, and left there it would be a reading on a day the athlete
    /// was not weighed. One that differs was logged separately and stays.
    /// </remarks>
    private async Task<Result<ScanView, Problem>> RestatedAsync(
        BodyCompositionScan scan, StatedScan stated, CancellationToken ct)
    {
        if (scan.Day != stated.Day)
            await UnstageScansBodyweightAsync(scan, ct);

        scan.Restate(stated.Day, stated.Time, stated.Sheet);
        await StageBodyweightAsync(scan.UserId, stated, ct);
        await _context.StageRecentStateInvalidationAsync(scan.UserId, ct);

        return await SavedAsync(scan, ct);
    }

    private async Task UnstageScansBodyweightAsync(BodyCompositionScan scan, CancellationToken ct)
    {
        var written = await _context.BodyweightMeasurements
            .FirstOrDefaultAsync(m => m.UserId == scan.UserId && m.Day == scan.Day, ct);
        if (written is not null && written.Bodyweight == new Bodyweight(scan.Sheet.Weight, scan.Sheet.Unit))
            _context.BodyweightMeasurements.Remove(written);
    }

    /// <summary>
    /// The one save, where a scan at a minute already recorded — the same scan sent twice, or a
    /// correction onto another scan's minute — is the unique index's refusal.
    /// </summary>
    private async Task<Result<ScanView, Problem>> SavedAsync(BodyCompositionScan scan, CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return Result<ScanView, Problem>.Success(ScanView.Of(scan));
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: SqliteUniqueFailure } sqlite
                                          && sqlite.Message.Contains(BodyMeasurementConfiguration.ScanMinuteColumns))
        {
            return Result<ScanView, Problem>.Failure(new Problem.Conflict(
                $"A scan taken {scan.Day:yyyy-MM-dd} at {scan.Time:HH\\:mm} is already recorded."));
        }
    }

    /// <summary>SQLite's SQLITE_CONSTRAINT, which a unique index's refusal raises.</summary>
    private const int SqliteUniqueFailure = 19;

    private async Task StageBodyweightAsync(Guid userId, StatedScan stated, CancellationToken ct)
    {
        if (stated.RecordsBodyweight)
            await _context.StageBodyweightAsync(userId, stated.Day, stated.Bodyweight, ct);
    }

    private async Task<Unit> RemovedAsync(BodyCompositionScan scan, CancellationToken ct)
    {
        _context.BodyCompositionScans.Remove(scan);
        await _context.StageRecentStateInvalidationAsync(scan.UserId, ct);
        await _context.SaveChangesAsync(ct);
        return Unit.Value;
    }

    // ---- Reads ----

    private async Task<IReadOnlyList<ScanView>> AllAsync(Guid userId, CancellationToken ct) =>
        [.. (await ScansAsync(userId, ct)).Select(ScanView.Of)];

    private Task<List<BodyCompositionScan>> ScansAsync(Guid userId, CancellationToken ct) =>
        _context.BodyCompositionScans
            .AsNoTracking()
            .Where(scan => scan.UserId == userId)
            .OrderBy(scan => scan.Day).ThenBy(scan => scan.Time)
            .ToListAsync(ct);

    private sealed record StatedScan(
        DateOnly Day, TimeOnly Time, BodyCompositionSheet Sheet, Bodyweight Bodyweight, bool RecordsBodyweight);
}
