using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Focus;
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

/// <summary>Reads the log, the pins and the cardio a span of windows needs, once, and scores each window.</summary>
[ToolProvider]
public sealed class TrainingScoreService : ITrainingScoreService
{
    /// <summary>
    /// The most weeks one call scores: 48, which with the four weeks before the first that its
    /// targets grow from keeps the cardio read inside one report.
    /// </summary>
    public const int MostWeeks = 48;

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
        Description = "Lionheart's own training score (0-100; not Oura's activity score) for the seven days ending on the range's last day, as a few lines: part by part with the measurements behind each: hard sets of push, pull and legs (35 points); zone-2 minutes (60-70% of maximum heart rate) and quality minutes — work bouts of 2 minutes or more at zone 4-5 (80% of maximum and above) in runs, rides and other endurance sessions holding at least 10 such minutes (25 points, unknown rather than zero with no heart rate; ring-detected games and dances never count). Each volume measurement has this week's target, grown 10% from the mean of the four weeks before and kept between a floor and the committed standard (8 sets a family, 150 zone-2 minutes, 40 quality minutes); half its points are paid against this week's target and half against the committed standard; each family's priority lift — pinned, or the most-trained compound lift of 8 weeks — its best estimated one-rep max against the 12 weeks before (20 points); and sessions in each of the last four 7-day blocks against 3 (20 points). With cardio unknown the score is what the other parts earned, out of 75. Also what the log says is up next, the patterns and muscle groups under their weekly hard-set target (secondary muscles count half), and the scores of the weeks ending on each Sunday of the range, at most the last 12.")]
    public Task<TrainingScoreDigest?> GetTrainingScoreForChatAsync(IdentityUser user, DayRange range)
    {
        var sundays = Sundays(range).TakeLast(ChatWeeks).ToList();
        return InputsAsync(user, Span([.. sundays, range.To]), CancellationToken.None)
            .MapAsync(async inputs => TrainingScoreDigest.Of(
                inputs.Score(range.To),
                TrainingVolumeAnalysis.Build(
                    inputs.Log, range.To, await _context.ReadinessOnAsync(inputs.UserId, range.To, CancellationToken.None)),
                [.. sundays.Select(inputs.Score).Select(WeeklyScore.Of)]))
            .MatchAsync(digest => (TrainingScoreDigest?)digest, _ => null);
    }

    /// <summary>Scores each window ending on the days given, from one read of everything they need.</summary>
    private Task<Result<IReadOnlyList<TrainingScore>, Problem>> ScoresAsync(
        IdentityUser user, IReadOnlyList<DateOnly> endings, CancellationToken ct) =>
        endings.Count is 0
            ? Task.FromResult(Result<IReadOnlyList<TrainingScore>, Problem>.Success([]))
            : InputsAsync(user, Span(endings), ct)
                .MapAsync(IReadOnlyList<TrainingScore> (inputs) => [.. endings.Select(inputs.Score)]);

    /// <summary>The days the windows ending on the days given cover, and the four weeks their targets grow from.</summary>
    private static DayRange Span(IReadOnlyList<DateOnly> endings) =>
        new(TrainingScoreAnalysis.Before(VolumeTargets.WindowEndingOn(endings.Min())).Min(block => block.From), endings.Max());

    private Task<Result<ScoreInputs, Problem>> InputsAsync(IdentityUser user, DayRange span, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => CardioWindows.ReadAsync(_context, _cardio, user, span, ct)
                .MapAsync(async cardio => new ScoreInputs(
                    userId,
                    await _context.ReadAsync(userId, span.To, _map, ct),
                    await _context.PinsOfAsync(userId, ct),
                    cardio)));

    /// <summary>Everything a span of windows is scored from, read once.</summary>
    private sealed record ScoreInputs(Guid UserId, TrainingLog Log, IReadOnlyList<FocusPin> Pins, CardioWindows Cardio)
    {
        public TrainingScore Score(DateOnly endingOn) =>
            TrainingScoreAnalysis.Score(Log, Pins, Cardio.For, endingOn);
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
