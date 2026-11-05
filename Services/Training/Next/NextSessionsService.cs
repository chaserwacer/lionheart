using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Expectations;
using lionheart.Model.Training.Next;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sets;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.Score;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services.Training.Next;

public interface INextSessionsService
{
    /// <summary>The next lift, the next cardio and which comes first, as of a day.</summary>
    Task<Result<NextSessions, Problem>> GetNextAsync(IdentityUser user, DateOnly day, CancellationToken ct);
}

/// <summary>Reads the log, the score, two weeks of cardio and the night, and hands them to <see cref="NextSessionsAnalysis"/>.</summary>
/// <remarks>
/// The Train tab, the workout tracker's header and the chat briefing all read this one answer, so
/// "what next" is never worked out twice.
/// </remarks>
[ToolProvider]
public sealed class NextSessionsService : INextSessionsService
{
    /// <summary>The days of cardio read for the last quality session.</summary>
    public const int RecentCardioDays = 14;

    /// <summary>The nights a usual HRV is drawn from.</summary>
    public const int HrvBaselineNights = 28;

    private readonly ModelContext _context;
    private readonly ITrainingScoreService _score;
    private readonly ICardioReportService _cardio;
    private readonly MovementPatternMap _map;

    public NextSessionsService(ModelContext context, ITrainingScoreService score, ICardioReportService cardio, MovementPatternMap map)
    {
        _context = context;
        _score = score;
        _cardio = cardio;
        _map = map;
    }

    public Task<Result<NextSessions, Problem>> GetNextAsync(IdentityUser user, DateOnly day, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => _score.GetScoreAsync(user, day, ct)
                .BindAsync(score => _cardio.GetReportAsync(user, new DayRange(day.AddDays(1 - RecentCardioDays), day), ct)
                    .MapAsync(async cardio =>
                    {
                        return NextSessionsAnalysis.Of(new NextInputs(
                            day,
                            await _context.ReadAsync(userId, day, _map, ct),
                            score,
                            cardio.Sessions,
                            await RecoveryAsync(userId, day, ct)));
                    })));

    [Tool(Name = "GetNextSessions",
        Description = "What Tempo recommends training next as of the range's last day, as a few lines: which comes first (lift or cardio) and why; the next lift (the push/pull/legs family trained longest ago, with each due family's priority lift and its next top set: the last session's best set plus 5 lb / 2.5 kg, or one more rep on bodyweight lifts, against the 12-week best estimate — or that set held when today's going-in rating is under 65, the last top set was RPE 9.5+, the last session of it was rated 5/5 difficulty, or the week's latest session was rated 4/5+ accumulated fatigue — with a pinned target's distance); the next cardio (zone 2 for a number of minutes, or a quality interval session such as 4 x 4 min at zone 4-5) and why; and the recovery read (Oura readiness against its usual range, last night's HRV against the 28 nights before, sleep). Each with its reasons.")]
    public Task<IReadOnlyList<string>?> GetNextSessionsForChatAsync(IdentityUser user, DayRange range) =>
        GetNextAsync(user, range.To, CancellationToken.None)
            .MatchAsync(next => (IReadOnlyList<string>?)Lines(next), _ => null);

    /// <summary>The recommendation as the lines chat and the briefing read.</summary>
    public static IReadOnlyList<string> Lines(NextSessions next) =>
    [
        $"next first: {(next.First.Session is NextKind.Lift ? "lift" : "cardio")} — {string.Join(" ", next.First.Reasons)}",
        "next lift: " + next.Lift switch
        {
            NextLift.Train train => string.Join(" and ", train.Families.Select(PatternWords.Of))
                                    + string.Concat(train.Priorities.Select(priority => $"; {PriorityAimWords.Line(priority)}")),
            NextLift.NothingLogged => "nothing classified logged",
            _ => throw new System.Diagnostics.UnreachableException(),
        } + $" — {string.Join(" ", next.Lift.Reasons)}",
        "next cardio: " + next.Cardio switch
        {
            NextCardio.Zone2 zone2 => $"zone 2, {zone2.Minutes} min",
            NextCardio.Quality quality =>
                $"quality, {quality.Bouts} x {quality.BoutMinutes} min at zone 4-5 with {quality.RecoveryMinutes} min easy between ({quality.WorkMinutes} min of work)",
            _ => throw new System.Diagnostics.UnreachableException(),
        } + $" — {string.Join(" ", next.Cardio.Reasons)}",
    ];

    /// <summary>The day's readiness, and the main sleep filed under it against the nights before.</summary>
    private async Task<RecoveryRead> RecoveryAsync(Guid userId, DateOnly day, CancellationToken ct)
    {
        var nights = (await _context.OuraSleepPeriods
                .AsNoTracking()
                .Where(period => period.UserId == userId && period.Day >= day.AddDays(-HrvBaselineNights) && period.Day <= day)
                .ToListAsync(ct))
            .Where(period => period.Reading.IsMainSleep)
            .GroupBy(period => period.Day)
            .Select(night => night.MaxBy(period => period.Reading.TotalSleepSeconds)!)
            .ToList();

        var last = nights.FirstOrDefault(night => night.Day == day);
        var before = nights.Where(night => night.Day < day).Select(night => night.Reading.AverageHrv).OfType<int>().ToList();
        var usual = before.Count >= NextSessionsAnalysis.FewestHrvNights ? before.Average() : (double?)null;
        var spread = usual is { } mean ? Math.Sqrt(before.Average(hrv => (hrv - mean) * (hrv - mean))) : (double?)null;

        return new RecoveryRead(
            await _context.ReadinessOnAsync(userId, day, ct),
            last is null ? null : last.Reading.TotalSleepSeconds / 60,
            last?.Reading.AverageHrv,
            usual,
            spread);
    }
}
