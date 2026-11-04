using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.BodyMeasurements;
using lionheart.Model.Health.Measurements;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Profile;
using lionheart.Services.Training.Balance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health.Measurements;

public interface IHealthBodyweightImportService
{
    Task<Result<HealthBodyweightImportReport, Problem>> ImportAsync(
        IdentityUser user, HealthBodyweightsImport import, CancellationToken ct);
}

/// <summary>
/// Fills the days the athlete has no bodyweight for from the weigh-ins Health holds
/// (<see cref="WeighInPlan"/> has the rules).
/// </summary>
/// <remarks>
/// <b>Nothing stored is replayed.</b> A bodyweight changes the system load of a weighted dip or
/// pull-up, but nothing persisted holds a system load: personal records are on the logged weight,
/// and the priority report and the score add bodyweight to the plate on every read
/// (<see cref="TrainingLog.LoadOf"/>). So the import reports how many of those sets now read a
/// different bodyweight, which is how much history it re-read, and writes only the bodyweights.
/// </remarks>
public sealed class HealthBodyweightImportService : IHealthBodyweightImportService
{
    private readonly ModelContext _context;

    public HealthBodyweightImportService(ModelContext context) => _context = context;

    public Task<Result<HealthBodyweightImportReport, Problem>> ImportAsync(
        IdentityUser user, HealthBodyweightsImport import, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct).MapAsync(userId => ImportedAsync(userId, import.WeighIns ?? [], ct));

    private async Task<HealthBodyweightImportReport> ImportedAsync(
        Guid userId, IReadOnlyList<WeighInCommand> weighIns, CancellationToken ct)
    {
        var before = await MeasuredAsync(userId, ct);
        var lifts = await LiftsAsync(userId, ct);

        var plan = WeighInPlan.Of(
            weighIns,
            before.Select(measured => measured.Day).ToHashSet(),
            WeighInPlan.UnitFor(before.LastOrDefault(), lifts.Select(lift => lift.Set.Unit)));

        if (plan.Added.Count > 0)
        {
            _context.BodyweightMeasurements.AddRange(
                plan.Added.Select(added => BodyweightMeasurement.Measured(userId, added.Day, added.Bodyweight)));
            await _context.StageRecentStateInvalidationAsync(userId, ct);
            await _context.SaveChangesAsync(ct);
        }

        return new HealthBodyweightImportReport(
            plan.Added.Count,
            plan.DaysAlreadyMeasured,
            plan.Unreadable,
            WeighInPlan.Reweighed(
                lifts.Where(lift => lift.AddsToBodyweight).Select(lift => (lift.Day, lift.Set.Unit)),
                before,
                [.. before, .. plan.Added]));
    }

    /// <summary>Every bodyweight the athlete has, oldest first.</summary>
    private async Task<IReadOnlyList<BodyweightOnDay>> MeasuredAsync(Guid userId, CancellationToken ct) =>
    [
        .. (await _context.BodyweightMeasurements
                .AsNoTracking()
                .Where(measured => measured.UserId == userId)
                .OrderBy(measured => measured.Day)
                .ToListAsync(ct))
            .Select(measured => new BodyweightOnDay(measured.Day, measured.Bodyweight))
    ];

    private sealed record LoggedLift(DateOnly Day, LiftSet Set, bool AddsToBodyweight);

    /// <summary>Every lift set the athlete has completed, on its day, and whether its plate adds to bodyweight.</summary>
    private async Task<IReadOnlyList<LoggedLift>> LiftsAsync(Guid userId, CancellationToken ct)
    {
        var addsToBodyweight = (await _context.MovementDatas
                .AsNoTracking()
                .Where(movement => movement.UserID == userId && movement.Setup.AddsToBodyweight)
                .Select(movement => movement.MovementDataID)
                .ToListAsync(ct))
            .ToHashSet();

        return
        [
            .. from session in await _context.Sessions.OfType<CompletedTrainingSession>()
                   .AsNoTracking()
                   .Where(session => session.UserId == userId)
                   .ToListAsync(ct)
               from set in session.Sets.OfType<LiftSet>()
               select new LoggedLift(session.Date, set, addsToBodyweight.Contains(set.MovementDataId))
        ];
    }
}
