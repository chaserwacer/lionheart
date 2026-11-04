using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Health.Measurements;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health.Measurements;

public interface IHealthMeasureService
{
    Task<Result<HealthMeasuresImportReport, Problem>> ImportAsync(
        IdentityUser user, HealthMeasuresImport import, CancellationToken ct);

    Task<Result<IReadOnlyList<HealthMeasureSeries>, Problem>> GetRangeAsync(
        IdentityUser user, DayRange range, CancellationToken ct);
}

/// <summary>
/// The figures Apple's devices measure — the watch's VO2max, resting heart rate, HRV and breathing,
/// the phone's gait — kept a day at a time.
/// </summary>
/// <remarks>
/// An import restates a day it already holds: the phone re-reads the last week on every sync,
/// because the watch hands over a night's readings late, so offering a day again is the normal
/// case rather than a conflict. A day whose numbers cannot be readings of their figure is left out
/// and counted, and the rest of the batch is kept — one bad day must not stall six years of them.
/// </remarks>
public sealed class HealthMeasureService : IHealthMeasureService
{
    private readonly ModelContext _context;

    public HealthMeasureService(ModelContext context) => _context = context;

    public Task<Result<HealthMeasuresImportReport, Problem>> ImportAsync(
        IdentityUser user, HealthMeasuresImport import, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .MapAsync(userId => KeptAsync(userId, (import.Days ?? []).Partition(day => day.Stated()), ct));

    public Task<Result<IReadOnlyList<HealthMeasureSeries>, Problem>> GetRangeAsync(
        IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered().MapAsync(ordered => SeriesAsync(Guid.Parse(user.Id), ordered, ct));

    // ---- Stage and commit ----

    private async Task<HealthMeasuresImportReport> KeptAsync(
        Guid userId, Partitioned<MeasuredDay, Problem> offered, CancellationToken ct)
    {
        // A day sent twice in one batch is one day; the later copy is the phone's later read of it.
        var days = offered.Delivered
            .GroupBy(day => (day.Metric, day.Device, day.Day))
            .Select(group => group.Last())
            .ToList();

        var held = await HeldAsync(userId, days, ct);
        var placed = days.Select(day => Place(day, held)).ToList();

        foreach (var placement in placed)
        {
            switch (placement)
            {
                case Placement.New fresh:
                    _context.HealthDailyMeasures.Add(HealthDailyMeasure.Measured(userId, fresh.Day));
                    break;
                case Placement.Changed changed:
                    changed.Measure.Restate(changed.Day.Aggregate);
                    break;
            }
        }
        await _context.SaveChangesAsync(ct);

        return new HealthMeasuresImportReport(
            Recorded: placed.OfType<Placement.New>().Count(),
            Restated: placed.OfType<Placement.Changed>().Count(),
            Unchanged: placed.OfType<Placement.Same>().Count(),
            Unreadable: offered.Refused.Count);
    }

    /// <summary>Where an offered day lands against what is held.</summary>
    private abstract record Placement
    {
        private Placement() { }

        public sealed record New(MeasuredDay Day) : Placement;

        public sealed record Changed(HealthDailyMeasure Measure, MeasuredDay Day) : Placement;

        public sealed record Same : Placement;
    }

    private static Placement Place(
        MeasuredDay day, IReadOnlyDictionary<(HealthMetric, MeasuringDevice, DateOnly), HealthDailyMeasure> held) =>
        !held.TryGetValue((day.Metric, day.Device, day.Day), out var measure) ? new Placement.New(day)
        : measure.Aggregate == day.Aggregate ? new Placement.Same()
        : new Placement.Changed(measure, day);

    /// <summary>The days already held among those offered, read in one query over the batch's span.</summary>
    private async Task<Dictionary<(HealthMetric, MeasuringDevice, DateOnly), HealthDailyMeasure>> HeldAsync(
        Guid userId, IReadOnlyList<MeasuredDay> days, CancellationToken ct)
    {
        if (days.Count is 0) return [];

        var (from, to) = (days.Min(day => day.Day), days.Max(day => day.Day));
        var metrics = days.Select(day => day.Metric).Distinct().ToList();

        return await _context.HealthDailyMeasures
            .Where(measure => measure.UserId == userId
                              && measure.Day >= from && measure.Day <= to
                              && metrics.Contains(measure.Metric))
            .ToDictionaryAsync(measure => (measure.Metric, measure.Device, measure.Day), ct);
    }

    // ---- Reads ----

    private async Task<IReadOnlyList<HealthMeasureSeries>> SeriesAsync(Guid userId, DayRange range, CancellationToken ct) =>
    [
        .. (await _context.HealthDailyMeasures
                .AsNoTracking()
                .Where(measure => measure.UserId == userId && measure.Day >= range.From && measure.Day <= range.To)
                .ToListAsync(ct))
            .GroupBy(measure => (measure.Metric, measure.Device))
            .OrderBy(series => series.Key.Metric)
            .ThenBy(series => series.Key.Device)
            .Select(series => new HealthMeasureSeries(
                series.Key.Metric,
                series.Key.Device,
                [.. series.OrderBy(measure => measure.Day).Select(MeasuredDayView.Of)]))
    ];
}
