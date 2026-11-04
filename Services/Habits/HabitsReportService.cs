using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Habits;
using lionheart.Model.Oura;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Habits;

public interface IHabitsReportService
{
    /// <summary>
    /// The habits report for a range of days and the equal span before it, or a refusal when the
    /// range runs backwards or spans more than <see cref="HabitsReportService.MaximumDays"/> days.
    /// </summary>
    Task<Result<HabitsReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct);

    /// <summary>One day as a timeline, or a refusal when no activity document is stored for it.</summary>
    Task<Result<HabitsDayDetail, Problem>> GetDayAsync(IdentityUser user, DateOnly day, CancellationToken ct);
}

/// <summary>
/// Reads what the habits report needs and hands it to <see cref="HabitsAnalysis"/>.
/// </summary>
[ToolProvider]
public sealed class HabitsReportService : IHabitsReportService
{
    /// <summary>The longest range one report covers — a year and a day — compared against the same before it.</summary>
    public const int MaximumDays = 367;

    private readonly ModelContext _context;

    public HabitsReportService(ModelContext context) => _context = context;

    public Task<Result<HabitsReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered()
            .Ensure(
                ordered => ordered.Days <= MaximumDays,
                _ => new Problem.Invalid(nameof(DayRange.To), $"A habits report spans at most {MaximumDays} days."))
            .BindAsync(ordered => _context.AthleteIdAsync(user, ct).MapAsync(userId => (userId, ordered)))
            .MapAsync(asked => InputsAsync(
                asked.userId, asked.ordered, new DayRange(HabitsAnalysis.PreviousOf(asked.ordered).From, asked.ordered.To), ct))
            .MapAsync(HabitsAnalysis.Build);

    public Task<Result<HabitsDayDetail, Problem>> GetDayAsync(IdentityUser user, DateOnly day, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .MapAsync(userId => InputsAsync(userId, new DayRange(day, day), new DayRange(day, day), ct))
            .BindAsync(inputs => HabitsAnalysis.Detail(day, inputs) is { } detail
                ? Result<HabitsDayDetail, Problem>.Success(detail)
                : Result<HabitsDayDetail, Problem>.Failure(
                    new Problem.NotFound($"No Oura activity is stored for {day:yyyy-MM-dd}.")));

    [Tool(Name = "GetHabitsReport",
        Description = "How the athlete's days are shaped, from Oura's five-minute activity classification: per day, steps, active and sedentary minutes, the longest unbroken waking sitting and the count of sittings of an hour or more, and bedtime and wake time; and for the range and the equal span before it, weekday, weekend and all-day means of those, an hour-by-hour profile of the mean day (asleep, sedentary, low, medium, high minutes, and training sessions begun), and sleep timing with its night-to-night spread. At most 92 days per call — a longer range returns its most recent 92 days.")]
    public Task<HabitsReport?> GetHabitsReportForChatAsync(IdentityUser user, DayRange range) =>
        GetReportAsync(user, Recent(range), CancellationToken.None)
            .MatchAsync(report => (HabitsReport?)report, _ => null);

    private const int ChatDayLimit = 92;

    private static DayRange Recent(DayRange range) =>
        range.Days > ChatDayLimit ? new DayRange(range.To.AddDays(1 - ChatDayLimit), range.To) : range;

    // ---- Reads ----

    private async Task<HabitsInputs> InputsAsync(Guid userId, DayRange asked, DayRange read, CancellationToken ct)
    {
        // An activity day runs 04:00 to 04:00, so its sleep and training can begin the evening before
        // and end the morning after: the event window reaches a day past both ends.
        var window = new TimeWindow(
            new DateTimeOffset(read.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddDays(-2),
            new DateTimeOffset(read.To.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero).AddDays(2));
        var (fromUtc, toUtc) = (window.From.UtcDateTime, window.To.UtcDateTime);

        var activity = await _context.OuraDailyActivity
            .AsNoTracking()
            .Where(document => document.UserId == userId && document.Day >= read.From && document.Day <= read.To)
            .ToListAsync(ct);

        var sleep = await _context.OuraSleepPeriods
            .AsNoTracking()
            .Where(period => period.UserId == userId)
            .Where(period => EF.Property<DateTime>(period, "StartTime") >= fromUtc
                             && EF.Property<DateTime>(period, "StartTime") <= toUtc)
            .ToListAsync(ct);

        var ringWorkouts = await _context.OuraWorkouts
            .AsNoTracking()
            .Where(workout => workout.UserId == userId)
            .Where(workout => EF.Property<DateTime>(workout, "StartTime") >= fromUtc
                              && EF.Property<DateTime>(workout, "StartTime") <= toUtc)
            .ToListAsync(ct);

        var logged = await _context.Sessions.OfType<CompletedTrainingSession>()
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .Where(session => EF.Property<DateTime>(session, "StartTime") >= fromUtc
                              && EF.Property<DateTime>(session, "StartTime") <= toUtc)
            .ToListAsync(ct);

        return new HabitsInputs(asked, activity, sleep, Training(ringWorkouts, logged));
    }

    /// <summary>
    /// Every training session as a span on the clock: each logged session, and each ring workout the
    /// cardio report counts as a session of its own.
    /// </summary>
    private static IReadOnlyList<TrainingSpan> Training(
        IReadOnlyList<OuraWorkout> ringWorkouts, IReadOnlyList<CompletedTrainingSession> logged) =>
    [
        .. logged
            .Select(session => session.Period is Interval interval
                ? new TrainingSpan(LoggedActivity(session), interval.Start, interval.End, IsLogged: true)
                : null)
            .OfType<TrainingSpan>(),
        .. CardioAnalysis.Gather(ringWorkouts, logged).Sessions
            .Select(session => session.Source switch
            {
                CardioSessionSource.RingWorkout ring => new TrainingSpan(ring.Activity, session.Start, session.End, IsLogged: false),
                // Already listed above, from the log itself.
                CardioSessionSource.LoggedSession => null,
                _ => throw new System.Diagnostics.UnreachableException(),
            })
            .OfType<TrainingSpan>(),
    ];

    /// <summary>What a logged session was, in the same camel-cased style as Oura's activity names.</summary>
    private static string LoggedActivity(CompletedTrainingSession session) =>
        session.Sets.OfType<TrailRunSet>().Any() ? "trailRun"
        : session.Sets.EnduranceActivity is { } endurance
            ? endurance switch
            {
                EnduranceActivity.Ride => "ride",
                EnduranceActivity.IndoorRide => "indoorRide",
                EnduranceActivity.Hike => "hike",
                EnduranceActivity.Walk => "walk",
                EnduranceActivity.IndoorRun => "indoorRun",
                _ => throw new System.Diagnostics.UnreachableException(),
            }
        : session.Sets.OfType<DistanceTimeSet>().Any() ? "distanceAndTime"
        : "strengthTraining";
}
