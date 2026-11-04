using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Cardio;
using lionheart.Services.Training.Patterns;
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

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    private async Task<T> ScoreAsync<T>(Func<TrainingScoreService, Task<T>> act)
    {
        using var context = _athletes.CreateContext();
        return await act(new TrainingScoreService(context, new CardioReportService(context), MovementPatternMap.Shipped));
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
        Assert.Contains("push 3/8", digest.Parts[0]);
        Assert.StartsWith("cardio unknown", digest.Parts[1]);
        Assert.Contains("of 75 that could be read", digest.Score);
        // The families due and the day's own lifting are separate statements, so neither reads as the other.
        Assert.StartsWith("next: pull and legs (never trained)", digest.UpNext);
        Assert.Contains($"no hard sets logged on {Sunday.AddDays(2):yyyy-MM-dd}", digest.UpNext);
        Assert.Equal(12, digest.Weeks.Split(", ").Length);
        // About four characters a token: a whole briefing stays within a few hundred tokens.
        Assert.InRange(System.Text.Json.JsonSerializer.Serialize(digest).Length, 1, 2400);
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
}
