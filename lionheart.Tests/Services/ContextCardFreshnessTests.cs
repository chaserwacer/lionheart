using lionheart.Model.BodyMeasurements;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Profile;
using lionheart.Model.Training.Sets;
using lionheart.Services;
using lionheart.Services.Profile;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// The context card states only what the records still say: the body from dated measurements
/// rather than the profile's undated weight, and no injury status that would outlive the injury.
/// </summary>
public sealed class ContextCardFreshnessTests : IDisposable
{
    private readonly RemodelDatabase _db = new();
    private readonly FakeChatServer _server = new();

    public ContextCardFreshnessTests() => _server.RespondsWithTransportFailure();

    public void Dispose()
    {
        _server.Dispose();
        _db.Dispose();
    }

    private IdentityUser Athlete => new() { Id = _db.UserId.ToString() };

    private async Task<AthleteContextCard> CardAsync()
    {
        using var context = _db.CreateContext();
        var service = new AthleteContextCardService(context, new NarrativeChatClient(_server.CreateChatModel()), TimeProvider.System);
        return ResultAssert.Succeeded(await service.GetOrBuildCardAsync(Athlete));
    }

    [Fact]
    public async Task The_card_states_the_latest_dated_bodyweight_and_not_the_profiles_weight()
    {
        using (var context = _db.CreateContext())
        {
            (await context.LionheartUsers.SingleAsync()).Weight = 180;
            context.BodyweightMeasurements.Add(BodyweightMeasurement.Measured(
                _db.UserId, new DateOnly(2026, 9, 14), new Bodyweight(183.4, WeightUnit.Pounds)));
            await context.SaveChangesAsync();
        }

        var card = await CardAsync();

        Assert.DoesNotContain("180", card.StableProfileText);
        Assert.Contains("Bodyweight: 183.4 lb on 2026-09-14", card.RecentStateText);
    }

    /// <summary>
    /// Which injuries are active changes when one is resolved, so the card leaves it to the briefing
    /// read with each message, and the stable tier names only the history.
    /// </summary>
    [Fact]
    public async Task The_card_keeps_injury_history_and_leaves_whether_one_is_active_to_the_briefing()
    {
        await CreateInjuryAsync("Right knee tendinopathy");

        var card = await CardAsync();

        Assert.Contains("Right knee tendinopathy (2026-03)", card.StableProfileText);
        Assert.DoesNotContain("Active injuries", card.RecentStateText);
    }

    private async Task<Guid> CreateInjuryAsync(string name)
    {
        using var context = _db.CreateContext();
        var created = ResultAssert.Succeeded(await new InjuryService(context, TimeProvider.System).CreateInjuryAsync(Athlete, new CreateInjuryRequest
        {
            Name = name, InjuryDate = new DateTime(2026, 3, 2), Notes = string.Empty,
        }));
        return created.InjuryID;
    }
}
