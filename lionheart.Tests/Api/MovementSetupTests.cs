using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>How the athlete sets a movement up: its cues, and whether its weight adds to bodyweight.</summary>
public class MovementSetupTests : IDisposable
{
    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task A_movements_setup_is_replaced_trimmed_and_answered_on_the_movement()
    {
        var dip = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Dip");

        var view = ResultAssert.Succeeded(await SetUpAsync(dip, "  belt, 2 plates  ", addsToBodyweight: true));

        Assert.Equal(new MovementSetup("belt, 2 plates", true), view.Setup);
        using var context = _athletes.CreateContext();
        Assert.Equal(new MovementSetup("belt, 2 plates", true), (await context.MovementDatas.SingleAsync()).Setup);
    }

    [Fact]
    public async Task Setup_notes_past_two_thousand_characters_are_refused_and_nothing_changes()
    {
        var dip = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Dip");

        var refusal = ResultAssert.Failed(await SetUpAsync(dip, new string('x', 2001), addsToBodyweight: true));

        Assert.Equal("Notes", Assert.Single(Assert.IsType<Problem.Invalid>(refusal).Fields).Field);
        using var context = _athletes.CreateContext();
        Assert.Equal(MovementSetup.None, (await context.MovementDatas.SingleAsync()).Setup);
    }

    [Fact]
    public async Task Two_thousand_characters_of_notes_are_a_setup()
    {
        var dip = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Dip");

        ResultAssert.Succeeded(await SetUpAsync(dip, new string('x', 2000), addsToBodyweight: false));
    }

    [Fact]
    public async Task Another_athletes_movement_is_not_found()
    {
        var theirs = await _athletes.PlantMovementAsync(_athletes.StrangerId, "Dip");

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await SetUpAsync(theirs, "mine now", addsToBodyweight: false)));
    }

    private Task<Result<MovementDataDTO, Problem>> SetUpAsync(Guid movement, string notes, bool addsToBodyweight) =>
        _athletes.MovementDataAsync(service => service.UpdateSetupAsync(
            _athletes.Owner, movement, new MovementSetupRequest(notes, addsToBodyweight), default));
}
