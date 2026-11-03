using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Oura;

/// <summary>
/// Reading the all-day heart-rate series back out of its one-run-per-UTC-day storage.
/// </summary>
/// <remarks>
/// Callers think in windows of instants — a workout, the athlete's own day — and never in the UTC
/// days the runs are stored under. This is the one place that translates between the two, so the
/// storage unit can stay an implementation detail.
/// </remarks>
internal static class HeartRateStore
{
    /// <summary>Every stored sample inside <paramref name="window"/>, oldest first.</summary>
    public static async Task<IReadOnlyList<HeartRateSample>> SamplesAsync(
        this DbSet<OuraHeartRateDay> table, Guid userId, TimeWindow window, CancellationToken ct)
    {
        var samples = await table.SamplesOnAsync(userId, DaysTouching(window), ct);
        return [.. samples.Within(window)];
    }

    /// <summary>
    /// Every stored sample on the given UTC days, oldest first — for a caller that will cut many
    /// windows out of one read rather than asking once per window.
    /// </summary>
    public static async Task<IReadOnlyList<HeartRateSample>> SamplesOnAsync(
        this DbSet<OuraHeartRateDay> table, Guid userId, IEnumerable<DateOnly> days, CancellationToken ct)
    {
        var wanted = days.Distinct().ToList();

        var stored = await table
            .AsNoTracking()
            .Where(day => day.UserId == userId && wanted.Contains(day.Day))
            .OrderBy(day => day.Day)
            .ToListAsync(ct);

        return [.. stored.SelectMany(day => day.Reading.Samples)];
    }

    /// <summary>The UTC days a window touches, both ends included.</summary>
    public static IEnumerable<DateOnly> DaysTouching(TimeWindow window)
    {
        var first = DateOnly.FromDateTime(window.From.UtcDateTime);
        var last = DateOnly.FromDateTime(window.To.UtcDateTime);

        return Enumerable.Range(0, last.DayNumber - first.DayNumber + 1).Select(first.AddDays);
    }

    /// <summary>The samples at or after the window's start and before its end.</summary>
    public static IEnumerable<HeartRateSample> Within(this IEnumerable<HeartRateSample> samples, TimeWindow window) =>
        samples.Where(sample => sample.At >= window.From && sample.At < window.To);
}
