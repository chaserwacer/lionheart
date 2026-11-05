using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.GoingIn;
using lionheart.Services;
using lionheart.Services.Cardio;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.GoingIn;
using lionheart.Services.Training.Patterns;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace lionheart.Tests.GoingIn;

/// <summary>
/// The going-in state read from a real database: the injuries come with the movements their
/// check-ins name, which is what the logging surface marks, and a resolved injury is not noted.
/// </summary>
public sealed class GoingInServiceTests : IDisposable
{
    private static readonly DateTimeOffset Evening = new(2026, 3, 10, 22, 0, 0, TimeSpan.Zero);

    private readonly RemodelDatabase _db = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _toolbox.Dispose();
        _db.Dispose();
    }

    private IdentityUser Athlete => new() { Id = _db.UserId.ToString() };

    private async Task<GoingInState> ReadAsync()
    {
        await using var context = _db.CreateContext();
        var facts = new TodayFactsService(
            context, new NoCardio(), MovementPatternMap.Shipped, new FixedClock(Evening, TimeZoneInfo.Utc), NullLogger<TodayFactsService>.Instance);
        var volume = new TrainingVolumeService(context, MovementPatternMap.Shipped, TimeProvider.System);
        return ResultAssert.Succeeded(await new GoingInService(context, facts, volume).GetAsync(Athlete, CancellationToken.None));
    }

    private async Task<Guid> InjureAsync(string name, int pain, params Guid[] movements)
    {
        await using var context = _db.CreateContext();
        var injuries = new InjuryService(context, new FixedClock(Evening, TimeZoneInfo.Utc));
        var injury = ResultAssert.Succeeded(await injuries.CreateInjuryAsync(Athlete, new CreateInjuryRequest
        {
            Name = name, InjuryDate = new DateTime(2026, 3, 1), Notes = string.Empty,
        })).InjuryID;
        ResultAssert.Succeeded(await injuries.CreateInjuryEventAsync(Athlete, new CreateInjuryEventRequest
        {
            InjuryID = injury, Notes = string.Empty, PainLevel = pain, InjuryType = InjuryEventType.checkin,
            MovementIDs = [.. movements],
        }));
        return injury;
    }

    [Fact]
    public async Task An_active_injury_is_noted_with_its_last_pain_and_the_movements_it_touches()
    {
        var squat = await _db.PlantMovementAsync("Squat", "Barbell");
        await InjureAsync("Left knee", 3, squat);

        var state = await ReadAsync();

        var injury = Assert.Single(state.Injuries);
        Assert.Equal("Left knee", injury.Name);
        Assert.Equal(3, injury.LastPain);
        Assert.Equal([squat], injury.MovementDataIds);
    }

    [Fact]
    public async Task A_resolved_injury_is_not_noted()
    {
        var injury = await InjureAsync("Left knee", 3);
        await using (var context = _db.CreateContext())
        {
            ResultAssert.Succeeded(await new InjuryService(context, new FixedClock(Evening, TimeZoneInfo.Utc)).UpdateInjuryAsync(Athlete, new UpdateInjuryRequest
            {
                InjuryID = injury, Name = "Left knee", Notes = string.Empty, IsActive = false,
            }));
        }

        Assert.Empty((await ReadAsync()).Injuries);
    }

    [Fact]
    public async Task An_athlete_with_no_ring_data_is_not_rated_and_unread_cardio_leaves_load_unscored()
    {
        var state = await ReadAsync();

        Assert.IsType<GoingInRating.NotRated>(state.Rating);
        Assert.IsType<PartScore.NotScored>(state.Load.Score);
    }

    private sealed class NoCardio : ICardioReportService
    {
        public Task<Result<CardioReport, Problem>> GetReportAsync(IdentityUser user, DayRange range, CancellationToken ct) =>
            Task.FromResult(Result<CardioReport, Problem>.Failure(new Problem.Unexpected("No cardio in this test.")));
    }
}
