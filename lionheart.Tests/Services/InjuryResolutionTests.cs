using lionheart.Model.InjuryManagement;
using lionheart.Services;
using lionheart.Services.Training.Expectations;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Services;

/// <summary>
/// The day an injury was resolved, through the real schema: stamped on the athlete's own day when
/// it is resolved, kept by resolving it again, cleared by reopening it, and absent on a row resolved
/// before the day was kept.
/// </summary>
public sealed class InjuryResolutionTests : IDisposable
{
    // Two in the morning UTC is still the evening before five hours west.
    private static readonly TimeZoneInfo FiveHoursWest = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");
    private static readonly ZonedClock EarlyUtc = new(new DateTimeOffset(2026, 3, 10, 2, 0, 0, TimeSpan.Zero), FiveHoursWest);
    private static readonly DateOnly HisDay = new(2026, 3, 9);

    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private IdentityUser Athlete => new() { Id = _db.UserId.ToString() };

    private async Task<Guid> InjureAsync()
    {
        await using var context = _db.CreateContext();
        return ResultAssert.Succeeded(await new InjuryService(context, EarlyUtc).CreateInjuryAsync(Athlete, new CreateInjuryRequest
        {
            Name = "Left knee", InjuryDate = new DateTime(2026, 2, 1), Notes = string.Empty,
        })).InjuryID;
    }

    private async Task SetActiveAsync(Guid injury, bool active, TimeProvider clock)
    {
        await using var context = _db.CreateContext();
        ResultAssert.Succeeded(await new InjuryService(context, clock).UpdateInjuryAsync(Athlete, new UpdateInjuryRequest
        {
            InjuryID = injury, Name = "Left knee", Notes = string.Empty, IsActive = active,
        }));
    }

    private async Task<InjuryStanding> StandingAsync()
    {
        await using var context = _db.CreateContext();
        return Assert.Single(await context.InjuryRecordsAsync(_db.UserId, CancellationToken.None)).Standing;
    }

    [Fact]
    public async Task A_new_injury_is_active()
    {
        await InjureAsync();

        Assert.IsType<InjuryStanding.Active>(await StandingAsync());
    }

    [Fact]
    public async Task Resolving_an_injury_keeps_the_athletes_own_day_not_the_utc_one()
    {
        var injury = await InjureAsync();

        await SetActiveAsync(injury, active: false, EarlyUtc);

        Assert.Equal(new InjuryStanding.Resolved(HisDay), await StandingAsync());
    }

    [Fact]
    public async Task Resolving_an_injury_already_resolved_keeps_the_day_it_was_first_resolved()
    {
        var injury = await InjureAsync();
        await SetActiveAsync(injury, active: false, EarlyUtc);

        await SetActiveAsync(injury, active: false, new ZonedClock(EarlyUtc.GetUtcNow().AddDays(5), FiveHoursWest));

        Assert.Equal(new InjuryStanding.Resolved(HisDay), await StandingAsync());
    }

    [Fact]
    public async Task Reopening_a_resolved_injury_clears_the_day_it_was_resolved()
    {
        var injury = await InjureAsync();
        await SetActiveAsync(injury, active: false, EarlyUtc);

        await SetActiveAsync(injury, active: true, EarlyUtc);

        Assert.IsType<InjuryStanding.Active>(await StandingAsync());
    }

    [Fact]
    public async Task A_row_resolved_before_the_day_was_kept_reads_as_resolved_undated_and_stays_so_when_resolved_again()
    {
        var injury = await InjureAsync();
        _db.ExecuteNonQuery($"UPDATE Injuries SET IsActive = 0 WHERE InjuryID = '{RemodelDatabase.SqlGuid(injury)}'");
        var read = await StandingAsync();

        await SetActiveAsync(injury, active: false, EarlyUtc);

        Assert.IsType<InjuryStanding.ResolvedUndated>(read);
        Assert.IsType<InjuryStanding.ResolvedUndated>(await StandingAsync());
    }

    private sealed class ZonedClock(DateTimeOffset utc, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
