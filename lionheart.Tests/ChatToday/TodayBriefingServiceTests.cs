using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.InjuryManagement;
using lionheart.Services;
using lionheart.Services.Cardio;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

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

    private async Task<string> BriefingAsync()
    {
        await using var context = _db.CreateContext();
        return await new TodayBriefingService(
                context, new NoCardio(), _toolbox.Executor, MovementPatternMap.Shipped, new ZonedClock(Evening, Zone),
                NullLogger<TodayBriefingService>.Instance)
            .ForAsync(Athlete, CancellationToken.None);
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

        Assert.Contains("Trained today: nothing logged yet.", briefing);
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
