using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Patterns;
using lionheart.Services;
using lionheart.Services.Training.Expectations;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Services;

/// <summary>
/// The families an injury touches, through the real schema: optional when it is logged, kept by an
/// update that does not mention them, and read back with the movements its check-ins name.
/// </summary>
public sealed class InjuryFamiliesTests : IDisposable
{
    private readonly RemodelDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private IdentityUser Athlete => new() { Id = _db.UserId.ToString() };

    private async Task<Guid> InjureAsync(params PatternFamily[] families)
    {
        await using var context = _db.CreateContext();
        return ResultAssert.Succeeded(await new InjuryService(context).CreateInjuryAsync(Athlete, new CreateInjuryRequest
        {
            Name = "Left knee", InjuryDate = new DateTime(2026, 3, 1), Notes = string.Empty, Families = [.. families],
        })).InjuryID;
    }

    private async Task<InjuryRecord> ReadAsync()
    {
        await using var context = _db.CreateContext();
        return Assert.Single(await context.InjuryRecordsAsync(_db.UserId, CancellationToken.None));
    }

    private async Task UpdateAsync(Guid injury, List<PatternFamily>? families)
    {
        await using var context = _db.CreateContext();
        ResultAssert.Succeeded(await new InjuryService(context).UpdateInjuryAsync(Athlete, new UpdateInjuryRequest
        {
            InjuryID = injury, Name = "Left knee", Notes = "better", IsActive = true, Families = families,
        }));
    }

    [Fact]
    public async Task An_injury_logged_without_families_names_none()
    {
        await InjureAsync();

        Assert.Empty((await ReadAsync()).Families);
    }

    [Fact]
    public async Task The_families_an_injury_is_logged_with_round_trip_once_each()
    {
        await InjureAsync(PatternFamily.Legs, PatternFamily.Legs, PatternFamily.Pull);

        Assert.Equal([PatternFamily.Legs, PatternFamily.Pull], (await ReadAsync()).Families);
    }

    [Fact]
    public async Task An_update_that_does_not_mention_families_keeps_them_and_one_that_does_replaces_them()
    {
        var injury = await InjureAsync(PatternFamily.Legs);

        await UpdateAsync(injury, families: null);
        var kept = await ReadAsync();
        await UpdateAsync(injury, families: []);
        var cleared = await ReadAsync();

        Assert.Equal([PatternFamily.Legs], kept.Families);
        Assert.Empty(cleared.Families);
    }

    [Fact]
    public async Task The_movements_its_check_ins_name_and_the_last_pain_are_read_with_it()
    {
        var squat = await _db.PlantMovementAsync("Squat", "Barbell");
        var injury = await InjureAsync();
        await using (var context = _db.CreateContext())
        {
            ResultAssert.Succeeded(await new InjuryService(context).CreateInjuryEventAsync(Athlete, new CreateInjuryEventRequest
            {
                InjuryID = injury, Notes = string.Empty, PainLevel = 4, InjuryType = InjuryEventType.checkin, MovementIDs = [squat],
            }));
        }

        var read = await ReadAsync();

        Assert.Equal([squat], read.Movements);
        Assert.Equal(4, read.LastPain);
    }
}
