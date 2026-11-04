using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Cardio;

/// <summary>
/// The cardio of any seven-day window inside a span, read once: one cardio report for the span and
/// the days that have heart rate.
/// </summary>
/// <remarks>
/// The cardio report is the one source of zone minutes, so the score and the Cardio screen can
/// never disagree about a session. Heart-rate days are stored by UTC day, which is a close enough
/// stand-in for the athlete's own day for "was the ring worn at all this week".
/// </remarks>
public sealed class CardioWindows
{
    private readonly CardioReport _report;
    private readonly IReadOnlySet<DateOnly> _heartRateDays;

    private CardioWindows(CardioReport report, IReadOnlySet<DateOnly> heartRateDays)
    {
        _report = report;
        _heartRateDays = heartRateDays;
    }

    public static Task<Result<CardioWindows, Problem>> ReadAsync(
        ModelContext context, ICardioReportService cardio, IdentityUser user, DayRange span, CancellationToken ct) =>
        cardio.GetReportAsync(user, span, ct)
            .MapAsync(async report => new CardioWindows(report, await HeartRateDaysAsync(context, user, span, ct)));

    public CardioMeasure For(DayRange window) =>
        CardioMeasures.Of(
            [.. _report.Sessions.Where(session => Within(window, DateOnly.FromDateTime(session.Start.DateTime)))],
            _report.MaxHeartRate,
            _heartRateDays.Count(day => Within(window, day)));

    private static async Task<IReadOnlySet<DateOnly>> HeartRateDaysAsync(
        ModelContext context, IdentityUser user, DayRange span, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var days = await context.OuraHeartRateDays
            .AsNoTracking()
            .Where(day => day.UserId == userId && day.Day >= span.From && day.Day <= span.To)
            .Select(day => day.Day)
            .ToListAsync(ct);
        return days.ToHashSet();
    }

    private static bool Within(DayRange range, DateOnly day) => day >= range.From && day <= range.To;
}
