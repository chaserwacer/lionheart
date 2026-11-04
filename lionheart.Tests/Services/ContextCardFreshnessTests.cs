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
/// rather than the profile's undated weight, and an injury's status from the injury records rather
/// than from whenever the stable tier was last built.
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
        var service = new AthleteContextCardService(context, new NarrativeChatClient(_server.CreateChatModel()));
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

    [Fact]
    public async Task An_injury_resolved_after_the_card_was_built_no_longer_reads_as_active()
    {
        var injury = await CreateInjuryAsync("Right knee tendinopathy");
        Assert.Contains("Active injuries: Right knee tendinopathy", (await CardAsync()).RecentStateText);

        using (var context = _db.CreateContext())
        {
            ResultAssert.Succeeded(await new InjuryService(context).UpdateInjuryAsync(Athlete, new UpdateInjuryRequest
            {
                InjuryID = injury, Name = "Right knee tendinopathy", Notes = string.Empty, IsActive = false,
            }));
        }

        var card = await CardAsync();
        Assert.Contains("Active injuries: none recorded.", card.RecentStateText);
        Assert.DoesNotContain("active)", card.StableProfileText);
    }

    private async Task<Guid> CreateInjuryAsync(string name)
    {
        using var context = _db.CreateContext();
        var created = ResultAssert.Succeeded(await new InjuryService(context).CreateInjuryAsync(Athlete, new CreateInjuryRequest
        {
            Name = name, InjuryDate = new DateTime(2026, 3, 2), Notes = string.Empty,
        }));
        return created.InjuryID;
    }
}
