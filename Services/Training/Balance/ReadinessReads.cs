using lionheart.Data;
using lionheart.Model.Training.Balance;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Balance;

/// <summary>Reads the day's readiness, and the ninety days its usual range is drawn from.</summary>
public static class ReadinessReads
{
    /// <summary>The day's readiness against the ninety days before it.</summary>
    public static async Task<ReadinessNote> ReadinessOnAsync(
        this ModelContext context, Guid userId, DateOnly day, CancellationToken ct)
    {
        var from = day.AddDays(-ReadinessNotes.UsualRangeDays);
        var days = await context.OuraDailyReadiness
            .AsNoTracking()
            .Where(readiness => EF.Property<Guid>(readiness, "UserId") == userId
                                && EF.Property<DateOnly>(readiness, "Day") >= from
                                && EF.Property<DateOnly>(readiness, "Day") <= day)
            .ToListAsync(ct);

        return ReadinessNotes.Of(
            [.. days.Where(readiness => readiness.Reading.Score is not null)
                .Select(readiness => (readiness.Day, readiness.Reading.Score.GetValueOrDefault()))],
            day);
    }
}
