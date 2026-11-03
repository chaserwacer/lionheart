using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Api;

/// <summary>The previous-performances read against real storage: whose sessions, and which states.</summary>
public class PreviousPerformanceServiceTests : IDisposable
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 2, 17, 0, 0, TimeSpan.FromHours(-5));

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task Previous_performances_read_only_the_athletes_own_completed_sessions()
    {
        var press = Guid.NewGuid();
        var mine = await LogAsync(Monday, press);
        await LogAsync(Monday.AddDays(1), press, _athletes.Stranger);
        await _athletes.SessionsAsync(async service =>
        {
            var active = ResultAssert.Succeeded(await service.StartUnplannedAsync(
                _athletes.Owner, new StartUnplannedSessionRequest(Monday.AddDays(2), "going", null, null), default));
            return await service.ReplaceSetsAsync(
                _athletes.Owner, active.TrainingSessionId,
                new ReplaceSetsRequest([new LiftSetCommand(press, 5, 300, WeightUnit.Pounds, null)]), default);
        });

        var performances = ResultAssert.Succeeded(await _athletes.PreviousPerformancesAsync(service =>
            service.GetAllAsync(_athletes.Owner, default)));

        var previous = Assert.Single(performances);
        Assert.Equal((press, mine), (previous.MovementDataId, previous.TrainingSessionId));
        Assert.Equal(Monday, previous.StartedAt);
    }

    private async Task<Guid> LogAsync(DateTimeOffset start, Guid movement, IdentityUser? athlete = null) =>
        ResultAssert.Succeeded(await _athletes.SessionsAsync(service => service.LogCompletedAsync(
            athlete ?? _athletes.Owner,
            new LogCompletedSessionRequest(
                [new LiftSetCommand(movement, 5, 225, WeightUnit.Pounds, null)],
                new PeriodCommand(start, start.AddHours(1)), null, "logged", null, null),
            default))).TrainingSessionId;
}
