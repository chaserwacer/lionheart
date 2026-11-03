using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Cardio;

public interface ICardioReportService
{
    /// <summary>
    /// The cardio report for a range of days, or a refusal when the range runs backwards or spans
    /// more than <see cref="CardioReportService.MaximumDays"/> days.
    /// </summary>
    Task<Result<CardioReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct);
}

/// <summary>
/// Reads what the cardio report needs and hands it to <see cref="CardioAnalysis"/>.
/// </summary>
/// <remarks>
/// The reads reach further back than the range asked for, because two figures do: the aerobic-base
/// comparison always covers the twelve weeks before the range's end, and the ring's peak heart rate
/// is read from a year of sessions. Heart rate is read only for the UTC days a cardio session
/// touches, not for every day in that year.
/// </remarks>
[ToolProvider]
public sealed class CardioReportService : ICardioReportService
{
    /// <summary>The longest range one report covers: a year and a day, so a 1Y span always fits.</summary>
    public const int MaximumDays = 367;

    private readonly ModelContext _context;

    public CardioReportService(ModelContext context) => _context = context;

    public Task<Result<CardioReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered()
            .Ensure(
                ordered => ordered.Days <= MaximumDays,
                _ => new Problem.Invalid(nameof(DayRange.To), $"A cardio report spans at most {MaximumDays} days."))
            .BindAsync(ordered => _context.AthleteIdAsync(user, ct).MapAsync(userId => (userId, ordered)))
            .MapAsync(asked => InputsAsync(asked.userId, asked.ordered, ct))
            .MapAsync(CardioAnalysis.Build);

    [Tool(Name = "GetCardioReport",
        Description = "The athlete's cardio for a range of days: every cardio session (ring-detected workouts and logged runs, with walks under 30 minutes, housework and lifting excluded and counted), each with average and peak heart rate and minutes in five heart-rate zones (percent of maximum heart rate: 50/60/70/80/90); weekly minutes, zone minutes and Edwards load; Oura's VO2max estimates, cardiovascular-age predictions and nightly lowest heart rate; and an aerobic-base comparison of the last four weeks against the eight before. States how maximum heart rate was set. At most 92 days per call — a longer range returns its most recent 92 days.")]
    public Task<CardioReport?> GetCardioReportForChatAsync(IdentityUser user, DayRange range) =>
        GetReportAsync(user, Recent(range), CancellationToken.None)
            .MatchAsync(report => (CardioReport?)report, _ => null);

    private const int ChatDayLimit = 92;

    private static DayRange Recent(DayRange range) =>
        range.Days > ChatDayLimit ? new DayRange(range.To.AddDays(1 - ChatDayLimit), range.To) : range;

    // ---- Reads ----

    private async Task<CardioInputs> InputsAsync(Guid userId, DayRange range, CancellationToken ct)
    {
        var read = new DayRange(CardioAnalysis.ReadFrom(range), range.To);
        var window = Spanning(read);

        var age = await _context.LionheartUsers
            .Where(athlete => athlete.UserID == userId)
            .Select(athlete => athlete.Age)
            .FirstAsync(ct);

        var ringWorkouts = (await Events(_context.OuraWorkouts, userId, window).ToListAsync(ct))
            .Where(workout => Within(read, workout.Day))
            .ToList();

        var logged = (await _context.Sessions.OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(session => session.UserId == userId)
                .Where(session => EF.Property<DateTime>(session, "StartTime") >= window.From.UtcDateTime
                                  && EF.Property<DateTime>(session, "StartTime") <= window.To.UtcDateTime)
                .ToListAsync(ct))
            .Where(session => Within(read, session.Date))
            .ToList();

        var mainSleeps = (await Events(_context.OuraSleepPeriods, userId, window).ToListAsync(ct))
            .Where(night => night.Reading.IsMainSleep && Within(read, night.Day))
            .ToList();

        // Gathered before the heart rate is read, so only the days a cardio session touches are
        // read — not every day the ring filed housework on.
        var gathered = CardioAnalysis.Gather(ringWorkouts, logged);
        var heartRate = await _context.OuraHeartRateDays.SamplesOnAsync(
            userId,
            gathered.Sessions.SelectMany(session => HeartRateStore.DaysTouching(new TimeWindow(session.Start, session.End))),
            ct);

        return new CardioInputs(
            range,
            age,
            gathered,
            heartRate,
            await Daily(_context.OuraVo2Max, userId, read).ToListAsync(ct),
            await Daily(_context.OuraDailyCardiovascularAge, userId, read).ToListAsync(ct),
            mainSleeps);
    }

    private static IQueryable<TDocument> Events<TDocument>(DbSet<TDocument> table, Guid userId, TimeWindow window)
        where TDocument : class, IOuraEvent =>
        table.AsNoTracking()
            .Where(document => EF.Property<Guid>(document, "UserId") == userId)
            .Where(document => EF.Property<DateTime>(document, "StartTime") >= window.From.UtcDateTime
                               && EF.Property<DateTime>(document, "StartTime") <= window.To.UtcDateTime);

    private static IQueryable<TDocument> Daily<TDocument>(DbSet<TDocument> table, Guid userId, DayRange range)
        where TDocument : class =>
        table.AsNoTracking()
            .Where(document => EF.Property<Guid>(document, "UserId") == userId)
            .Where(document => EF.Property<DateOnly>(document, "Day") >= range.From
                               && EF.Property<DateOnly>(document, "Day") <= range.To)
            .OrderBy(document => EF.Property<DateOnly>(document, "Day"))
            .ThenBy(document => EF.Property<Guid>(document, "OuraId"));

    /// <summary>
    /// A day range as instants, widened by a day at each end so an event the athlete lived on the
    /// first or last day is not cut off at a UTC midnight; callers filter back to the days.
    /// </summary>
    private static TimeWindow Spanning(DayRange range) =>
        new(new DateTimeOffset(range.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-1),
            new DateTimeOffset(range.To.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).AddDays(1));

    private static bool Within(DayRange range, DateOnly day) => day >= range.From && day <= range.To;
}
