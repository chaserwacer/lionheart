using lionheart.Functional;
using lionheart.Model.Analysis;
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

        Assert.Equal(4, score.Lifting.Families.Single(family => family.Family == PatternFamily.Push).HardSets);
        Assert.Equal(new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate), score.Cardio.Measure);
        Assert.Equal([ScorePart.Cardio], score.Unknown);
        Assert.Equal([1, 0, 0, 0], score.Consistency.SessionsPerBlock);
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
    public async Task The_chat_tool_hands_over_the_latest_score_its_volume_and_the_weeks_before()
    {
        await PlantBenchSessionAsync(Sunday.AddDays(-1), sets: 3);

        var briefing = Assert.IsType<TrainingScoreForChat>(await ScoreAsync(service =>
            service.GetTrainingScoreForChatAsync(_athletes.Owner, new DayRange(Sunday.AddDays(-13), Sunday.AddDays(2)))));

        Assert.Equal(Sunday.AddDays(2), briefing.Score.Window.To);
        Assert.Equal(3, briefing.Volume.Families.Single(family => family.Family == PatternFamily.Push).HardSets);
        Assert.Equal([Sunday.AddDays(-7), Sunday], briefing.Weeks.Select(week => week.Window.To));
    }
}
