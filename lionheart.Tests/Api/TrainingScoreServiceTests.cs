using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Model.InjuryManagement;
using lionheart.Services;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.GoingIn;
using Microsoft.Extensions.Logging.Abstractions;
using lionheart.Services.Training.Score;
using lionheart.Tests.Support;

namespace lionheart.Tests.Api;

/// <summary>
/// The training score through the real schema: the log, the map and the cardio read together, a
/// week with no heart rate scored as partial, the weeks route's limit, and the chat tool's briefing.
/// </summary>
public class TrainingScoreServiceTests : IDisposable
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-5);

    /// <summary>A Wednesday evening ten days after the Sunday, so no window scored here ends on today.</summary>
    private static readonly FixedClock Clock = new(new DateTimeOffset(2026, 11, 18, 20, 0, 0, Eastern));

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    private async Task<T> ScoreAsync<T>(Func<TrainingScoreService, Task<T>> act, TimeProvider? clock = null)
    {
        using var context = _athletes.CreateContext();
        var cardio = new CardioReportService(context);
        var goingIn = new GoingInService(
            context,
            new TodayFactsService(context, cardio, MovementPatternMap.Shipped, clock ?? Clock, NullLogger<TodayFactsService>.Instance),
            new TrainingVolumeService(context, MovementPatternMap.Shipped, clock ?? Clock));
        return await act(new TrainingScoreService(context, cardio, MovementPatternMap.Shipped, goingIn, clock ?? Clock));
    }

    private async Task PlantBenchSessionAsync(DateOnly day, int sets)
    {
        var bench = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Bench Press");
        var start = new DateTimeOffset(day.ToDateTime(new TimeOnly(18, 0)), Eastern);
        await _athletes.PlantAsync(context => context.Sessions.Add(TrainingHistory.Lifts(
            _athletes.OwnerId, start, bench, [.. Enumerable.Repeat((185d, 5), sets)])));
    }

    [Fact]
    public async Task A_week_with_no_heart_rate_counts_its_lifting_and_names_cardio_unknown()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-2), sets: 4);

        var score = ResultAssert.Succeeded(await ScoreAsync(service => service.GetScoreAsync(_athletes.Owner, Sunday, default)));

        Assert.Equal(4, score.Lifting.Families.Single(family => family.Of.Family == PatternFamily.Push).Of.HardSets);
        Assert.Equal(new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate), score.Cardio.Measure);
        Assert.Equal([ScorePart.Cardio], score.Unknown);
        Assert.Equal([1, 0, 0, 0], score.Consistency.Blocks.Select(block => block.Of.Sessions));
    }

    [Fact]
    public async Task Weeks_are_scored_one_per_sunday_and_more_than_a_year_is_refused_naming_to()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-9), sets: 2);

        var weeks = ResultAssert.Succeeded(await ScoreAsync(service =>
            service.GetWeeksAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-20), Sunday), default)));
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await ScoreAsync(service =>
            service.GetWeeksAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-7 * 52), Sunday), default))));

        Assert.Equal([Sunday.AddDays(-14), Sunday.AddDays(-7), Sunday], weeks.Select(week => week.Window.To));
        Assert.True(weeks[1].LiftingPoints > 0);
        Assert.Equal("To", Assert.Single(refusal.Fields).Field);
    }

    [Fact]
    public async Task The_chat_tool_hands_over_the_score_as_a_few_hundred_tokens_of_measurements()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-1), sets: 3);

        var digest = Assert.IsType<TrainingScoreDigest>(await ScoreAsync(service =>
            service.GetTrainingScoreForChatAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-85), Sunday.AddDays(2)))));

        Assert.Equal($"{Sunday.AddDays(-4):yyyy-MM-dd}..{Sunday.AddDays(2):yyyy-MM-dd}", digest.Window);
        Assert.StartsWith("lifting ", digest.Parts[0]);
        Assert.Contains("push 3/4 (committed 8", digest.Parts[0]);
        Assert.StartsWith("cardio unknown", digest.Parts[1]);
        Assert.Contains("of 75 that could be read", digest.Score);
        // The families due and the day's own lifting are separate statements, so neither reads as the other.
        Assert.StartsWith("next: pull and legs (never trained)", digest.UpNext);
        Assert.Contains($"no hard sets logged on {Sunday.AddDays(2):yyyy-MM-dd}", digest.UpNext);
        Assert.Equal(12, digest.Weeks.Split(", ").Length);
        Assert.StartsWith("push: Bench Press (Barbell) top set 5 x 190 lb", digest.Aims);
        // About four characters a token: a whole briefing stays within a few hundred tokens.
        Assert.InRange(System.Text.Json.JsonSerializer.Serialize(digest).Length, 1, 2400);
    }

    [Fact]
    public async Task A_day_marked_off_reaches_the_adjusted_score_and_the_chat_digest_with_its_note()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-1), sets: 3);
        ResultAssert.Succeeded(await _athletes.NonTrainingDaysAsync(Clock, service => service.MarkAsync(
            _athletes.Owner,
            new Model.Training.Availability.MarkNonTrainingDayRequest(Sunday.AddDays(-3), Note: "travel"),
            default)));

        var score = ResultAssert.Succeeded(await ScoreAsync(service => service.GetScoreAsync(_athletes.Owner, Sunday, default)));
        var digest = Assert.IsType<TrainingScoreDigest>(await ScoreAsync(service =>
            service.GetTrainingScoreForChatAsync(_athletes.Owner, new DayRange(Sunday, Sunday))));

        Assert.Equal(6, score.Adjusted.AvailableDays);
        Assert.True(score.Adjusted.Value >= score.Value);
        Assert.Contains($"6 of 7 days available (non-training {Sunday.AddDays(-3):yyyy-MM-dd} travel)", digest.Adjusted);
    }

    [Fact]
    public async Task Todays_window_is_scored_through_the_going_in_rating_and_aims_its_priority()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-2), sets: 4);
        var onTheSunday = new FixedClock(new DateTimeOffset(Sunday.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));

        var score = ResultAssert.Succeeded(await ScoreAsync(service => service.GetScoreAsync(_athletes.Owner, Sunday, default), onTheSunday));

        var push = Assert.Single(score.Expectations.Priorities);
        Assert.IsType<Model.Training.Expectations.LiftAim.Beat>(push.Aim);
        Assert.NotEmpty(push.Reasons);
    }

    [Fact]
    public async Task A_day_already_lifted_on_is_stated_apart_from_the_families_due_next()
    {
        await PlantBenchSessionAsync(Sunday, sets: 3);

        var digest = Assert.IsType<TrainingScoreDigest>(await ScoreAsync(service =>
            service.GetTrainingScoreForChatAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-6), Sunday))));

        Assert.Equal(
            $"next: pull and legs (never trained); {Sunday:yyyy-MM-dd} already has hard sets logged, so this is the session after it",
            digest.UpNext);
    }

    [Fact]
    public async Task An_injury_logged_with_its_family_pauses_that_family_in_the_score_and_the_chat_digest()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-2), sets: 3);
        await using (var context = _athletes.CreateContext())
        {
            ResultAssert.Succeeded(await new InjuryService(context, Clock).CreateInjuryAsync(_athletes.Owner, new CreateInjuryRequest
            {
                Name = "Left shoulder", InjuryDate = Sunday.AddDays(-10).ToDateTime(TimeOnly.MinValue), Notes = "", Families = [PatternFamily.Push],
            }));
        }

        var score = ResultAssert.Succeeded(await ScoreAsync(service => service.GetScoreAsync(_athletes.Owner, Sunday, default)));
        var digest = Assert.IsType<TrainingScoreDigest>(await ScoreAsync(service =>
            service.GetTrainingScoreForChatAsync(_athletes.Owner, new DayRange(Sunday, Sunday))));

        Assert.True(score.Expectations.Families.Single(family => family.Family == PatternFamily.Push).Paused);
        Assert.StartsWith($"Left shoulder since {Sunday.AddDays(-10):yyyy-MM-dd}, pausing push", digest.Injuries);
        Assert.StartsWith("Hold push while Left shoulder settles", digest.Advice[(digest.Advice.IndexOf(": ") + 2)..]);
    }
}
