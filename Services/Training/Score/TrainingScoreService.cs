using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Score;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Tools;

namespace lionheart.Services.Training.Score;

public interface ITrainingScoreService
{
    /// <summary>The score of the seven days ending on a day, part by part.</summary>
    Task<Result<TrainingScore, Problem>> GetScoreAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct);

    /// <summary>
    /// The score of every Monday-to-Sunday week whose Sunday falls in the range, oldest first, or a
    /// refusal for a range that runs backwards or holds more than <see cref="TrainingScoreService.MostWeeks"/> Sundays.
    /// </summary>
    Task<Result<IReadOnlyList<WeeklyScore>, Problem>> GetWeeksAsync(IdentityUser user, DayRange range, CancellationToken ct);
}

/// <summary>What the chat model is handed about the score: the latest, the volume behind it, and the weeks before.</summary>
public sealed record TrainingScoreForChat(TrainingScore Score, TrainingVolume Volume, IReadOnlyList<WeeklyScore> Weeks);

/// <summary>Reads the log, the pins and the cardio a span of windows needs, once, and scores each window.</summary>
[ToolProvider]
public sealed class TrainingScoreService : ITrainingScoreService
{
    /// <summary>The most weeks one call scores: a year, which keeps the cardio read inside one report.</summary>
    public const int MostWeeks = 52;

    private const int ChatWeeks = 12;

    private readonly ModelContext _context;
    private readonly ICardioReportService _cardio;
    private readonly MovementPatternMap _map;

    public TrainingScoreService(ModelContext context, ICardioReportService cardio, MovementPatternMap map)
    {
        _context = context;
        _cardio = cardio;
        _map = map;
    }

    public Task<Result<TrainingScore, Problem>> GetScoreAsync(IdentityUser user, DateOnly endingOn, CancellationToken ct) =>
        ScoresAsync(user, [endingOn], ct).MapAsync(scores => scores[0]);

    public Task<Result<IReadOnlyList<WeeklyScore>, Problem>> GetWeeksAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
        range.Ordered()
            .Map(Sundays)
            .Ensure(
                sundays => sundays.Count <= MostWeeks,
                _ => new Problem.Invalid(nameof(DayRange.To), $"At most {MostWeeks} weeks are scored at once."))
            .BindAsync(sundays => ScoresAsync(user, sundays, ct))
            .MapAsync(IReadOnlyList<WeeklyScore> (scores) => [.. scores.Select(WeeklyScore.Of)]);

    [Tool(Name = "GetTrainingScore",
        Description = "Lionheart's own training score (0-100; not Oura's activity score) for the seven days ending on the range's last day, part by part with the measurements behind each: hard sets of push, pull and legs against 8 each (35 points); zone-2 minutes against 150 and minutes at 80% of maximum heart rate or above against 20, from cardio sessions (25 points, unknown rather than zero with no heart rate); each family's priority lift — pinned, or the most-trained compound lift of 8 weeks — its best estimated one-rep max against the 12 weeks before (20 points); and sessions in each of the last four 7-day blocks against 4 (20 points). Also the same window's hard sets by family, movement pattern and muscle group (secondary muscles count half), the last day each was trained, what the log says is up next, and the scores of the weeks ending on each Sunday of the range, at most the last 12.")]
    public Task<TrainingScoreForChat?> GetTrainingScoreForChatAsync(IdentityUser user, DayRange range)
    {
        var sundays = Sundays(range).TakeLast(ChatWeeks).ToList();
        return _context.AthleteIdAsync(user, CancellationToken.None)
            .BindAsync(userId => ScoresAsync(user, [.. sundays, range.To], CancellationToken.None)
                .MapAsync(async scores => new TrainingScoreForChat(
                    scores[^1],
                    TrainingVolumeAnalysis.Build(
                        await _context.ReadAsync(userId, range.To, _map, CancellationToken.None),
                        range.To,
                        await _context.ReadinessOnAsync(userId, range.To, CancellationToken.None)),
                    [.. scores.Take(sundays.Count).Select(WeeklyScore.Of)])))
            .MatchAsync(briefing => (TrainingScoreForChat?)briefing, _ => null);
    }

    /// <summary>Scores each window ending on the days given, from one read of everything they need.</summary>
    private Task<Result<IReadOnlyList<TrainingScore>, Problem>> ScoresAsync(
        IdentityUser user, IReadOnlyList<DateOnly> endings, CancellationToken ct)
    {
        if (endings.Count is 0) return Task.FromResult(Result<IReadOnlyList<TrainingScore>, Problem>.Success([]));

        var span = new DayRange(VolumeTargets.WindowEndingOn(endings.Min()).From, endings.Max());
        return _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => CardioWindows.ReadAsync(_context, _cardio, user, span, ct)
                .MapAsync(async cardio =>
                {
                    var log = await _context.ReadAsync(userId, span.To, _map, ct);
                    var pins = await _context.PinsOfAsync(userId, ct);
                    return (IReadOnlyList<TrainingScore>)
                    [
                        .. endings.Select(day => TrainingScoreAnalysis.Score(
                            log, pins, cardio.For(VolumeTargets.WindowEndingOn(day)), day))
                    ];
                }));
    }

    /// <summary>Every Sunday in the range, oldest first.</summary>
    public static IReadOnlyList<DateOnly> Sundays(DayRange range)
    {
        var first = range.From.AddDays(((int)DayOfWeek.Sunday - (int)range.From.DayOfWeek + 7) % 7);
        return [.. Enumerable.Range(0, Math.Max(0, (range.To.DayNumber - first.DayNumber) / 7 + 1))
            .Select(week => first.AddDays(7 * week))
            .Where(sunday => sunday <= range.To)];
    }
}
