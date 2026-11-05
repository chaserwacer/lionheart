using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.InjuryManagement;
using lionheart.Services;
using lionheart.Services.Cardio;
using lionheart.Services.Llm;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Chat.Tools;
using Model.Tools;

namespace lionheart.Tests.ChatToday;

/// <summary>
/// The briefing read from a real database: training dated by the athlete's own day, and injury
/// status read fresh on every message rather than from whenever a card was built.
/// </summary>
public sealed class TodayBriefingServiceTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    /// <summary>21:40 on 10 March in New York, which is 01:40 on 11 March in UTC.</summary>
    private static readonly DateTimeOffset Evening = new(2026, 3, 11, 1, 40, 0, TimeSpan.Zero);

    private readonly RemodelDatabase _db = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _toolbox.Dispose();
        _db.Dispose();
    }

    private IdentityUser Athlete => new() { Id = _db.UserId.ToString() };

    private static TodayBriefingService Briefing(lionheart.Data.ModelContext context, ChatToolCallExecutor executor)
    {
        var clock = new ZonedClock(Evening, Zone);
        return new TodayBriefingService(
            new TodayFactsService(context, new NoCardio(), MovementPatternMap.Shipped, clock, NullLogger<TodayFactsService>.Instance),
            executor,
            clock,
            NullLogger<TodayBriefingService>.Instance);
    }

    private async Task<string> BriefingAsync()
    {
        await using var context = _db.CreateContext();
        return await Briefing(context, _toolbox.Executor).ForAsync(Athlete, CancellationToken.None);
    }

    [Fact]
    public async Task An_evening_session_counts_as_today_although_utc_has_moved_on()
    {
        var squat = await _db.PlantMovementAsync("Squat", "Barbell");
        await _db.CompleteInTurnAsync(TrainingHistory.Lifts(
            _db.UserId, new DateTimeOffset(2026, 3, 10, 19, 0, 0, TimeSpan.FromHours(-4)), squat, (225, 5), (225, 5)));

        var briefing = await BriefingAsync();

        Assert.Contains("Now: Tuesday 10 March 2026, 21:40 (UTC-04:00).", briefing);
        Assert.Contains("Trained today: lifting at 19:00, 60 min, 2 hard sets", briefing);
        Assert.Contains("(top 225 lb × 5)", briefing);
    }

    [Fact]
    public async Task Yesterdays_session_is_yesterdays()
    {
        var squat = await _db.PlantMovementAsync("Squat", "Barbell");
        await _db.CompleteInTurnAsync(TrainingHistory.Lifts(
            _db.UserId, new DateTimeOffset(2026, 3, 9, 18, 0, 0, TimeSpan.FromHours(-4)), squat, (205, 8)));

        var briefing = await BriefingAsync();

        Assert.Contains("Trained today: nothing logged yet", briefing);
        Assert.Contains("Yesterday (Monday 9 March): lifting at 18:00", briefing);
    }

    [Fact]
    public async Task An_injury_resolved_since_the_last_message_no_longer_reads_as_active()
    {
        Guid injury;
        await using (var context = _db.CreateContext())
        {
            injury = ResultAssert.Succeeded(await new InjuryService(context).CreateInjuryAsync(Athlete, new CreateInjuryRequest
            {
                Name = "Right knee tendinopathy", InjuryDate = new DateTime(2026, 3, 2), Notes = string.Empty,
            })).InjuryID;
        }
        Assert.Contains("Active injuries: Right knee tendinopathy", await BriefingAsync());

        await using (var context = _db.CreateContext())
        {
            ResultAssert.Succeeded(await new InjuryService(context).UpdateInjuryAsync(Athlete, new UpdateInjuryRequest
            {
                InjuryID = injury, Name = "Right knee tendinopathy", Notes = string.Empty, IsActive = false,
            }));
        }

        Assert.Contains("Active injuries: none.", await BriefingAsync());
    }

    /// <summary>The score tool is not in the test registry, so the score is unavailable and the rest still reads.</summary>
    [Fact]
    public async Task A_part_that_cannot_be_read_is_said_to_be_unavailable_and_the_rest_stands()
    {
        var briefing = await BriefingAsync();

        Assert.Contains("Training score, seven days ending today", briefing);
        Assert.EndsWith("not available.", briefing);
        Assert.Contains("Sleep: last night not synced yet", briefing);
    }

    /// <summary>
    /// The score is read by running the score tool itself, so the briefing states the week exactly
    /// as the tool would: the day's window reaches the tool as its own argument type, and its answer
    /// lands in the briefing verbatim.
    /// </summary>
    [Fact]
    public async Task The_score_is_the_score_tools_own_answer_for_the_seven_days_ending_today()
    {
        var score = new ScoreTool();
        var services = new ServiceCollection().AddSingleton<IScoreTool>(score).BuildServiceProvider();
        var tool = new ToolDescriptor(
            "GetTrainingScore", typeof(ScoreTool), typeof(ScoreTool).GetMethod(nameof(ScoreTool.ScoreAsync))!,
            new ToolDefinition("GetTrainingScore", "score", """{"type":"object"}"""), RequireAuthorization: true);
        var executor = new ChatToolCallExecutor(
            new ToolRegistry(new Dictionary<string, ToolDescriptor> { ["GetTrainingScore"] = tool }),
            services.GetRequiredService<IServiceScopeFactory>(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await using var context = _db.CreateContext();
        var briefing = await Briefing(context, executor).ForAsync(Athlete, CancellationToken.None);

        Assert.Equal(new DayRange(new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 10)), score.Asked);
        Assert.EndsWith("""{"score":"64 of 100"}""", briefing);
    }

    [Fact]
    public async Task Cardio_that_could_not_be_read_is_said_to_be_unread_and_not_reported_as_none()
    {
        var briefing = await BriefingAsync();

        Assert.Contains("Trained today: nothing logged yet (cardio could not be read).", briefing);
        Assert.Contains("Heart-rate zones: the cardio report could not be read", briefing);
    }

    public interface IScoreTool
    {
        Task<ScoreAnswer> ScoreAsync(IdentityUser user, DayRange range);
    }

    public sealed record ScoreAnswer(string Score);

    public sealed class ScoreTool : IScoreTool
    {
        public DayRange? Asked { get; private set; }

        public Task<ScoreAnswer> ScoreAsync(IdentityUser user, DayRange range)
        {
            Asked = range;
            return Task.FromResult(new ScoreAnswer("64 of 100"));
        }
    }

    private sealed class NoCardio : ICardioReportService
    {
        public Task<Result<CardioReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
            Task.FromResult(Result<CardioReport, Problem>.Failure(new Problem.Unexpected("No cardio in this test.")));
    }

    private sealed class ZonedClock(DateTimeOffset utc, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
