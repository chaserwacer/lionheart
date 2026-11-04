using lionheart.Functional;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Api;
using lionheart.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Persistence;

/// <summary>
/// How a focus pin reaches the database and comes back: its terms and its status as JSON, every
/// case of both, and the two foreign keys it carries.
/// </summary>
public class FocusPinPersistenceTests : IAsyncLifetime
{
    private static readonly DateTime PinnedAt = new(2026, 11, 3, 14, 15, 16, DateTimeKind.Utc);

    private readonly TwoAthletes _athletes = new();
    private Guid _dip;

    public async Task InitializeAsync() =>
        _dip = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Weighted Dip");

    public Task DisposeAsync()
    {
        _athletes.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task A_pin_round_trips_whichever_target_and_status_it_holds(bool liftTarget, bool unpinned)
    {
        var target = liftTarget
            ? ResultAssert.Succeeded(FocusTarget.Lift.Of(70, WeightUnit.Pounds, 5))
            : new FocusTarget.None();
        var pin = FocusPin.Pin(Guid.CreateVersion7(), _athletes.OwnerId, _dip, new FocusRole.Emphasis(), Terms(target), PinnedAt);
        if (unpinned) ResultAssert.Succeeded(pin.Unpin(new DateOnly(2026, 12, 20)));

        await _athletes.PlantAsync(context => context.FocusPins.Add(pin));
        var loaded = await LoadAsync(pin.FocusPinId);

        Assert.Equal(pin.Terms, loaded.Terms);
        Assert.Equal(pin.Status, loaded.Status);
        Assert.Equal(
            (pin.UserId, pin.MovementDataId, pin.PinnedAt),
            (loaded.UserId, loaded.MovementDataId, loaded.PinnedAt));
    }

    /// <summary>
    /// The stored shape is a contract with every row already written: renaming a member or a case
    /// silently orphans them, so the bytes are pinned here.
    /// </summary>
    [Fact]
    public async Task The_terms_and_status_are_stored_as_this_json()
    {
        var pin = FocusPin.Pin(
            Guid.CreateVersion7(), _athletes.OwnerId, _dip, new FocusRole.Emphasis(),
            Terms(ResultAssert.Succeeded(FocusTarget.Lift.Of(70, WeightUnit.Pounds, 5))), PinnedAt);
        ResultAssert.Succeeded(pin.Unpin(new DateOnly(2026, 12, 20)));
        await _athletes.PlantAsync(context => context.FocusPins.Add(pin));

        Assert.Equal(
            """{"Horizon":{"Starts":"2026-11-02","Ends":"2027-01-31"},"SessionsPerWeek":2,"Target":{"$type":"lift","Weight":70,"Unit":1,"Reps":5}}""",
            await StoredAsync(context => context.Database.SqlQuery<string>(
                $"select Terms as Value from FocusPins where FocusPinId = {pin.FocusPinId}")));
        Assert.Equal(
            """{"$type":"unpinned","On":"2026-12-20"}""",
            await StoredAsync(context => context.Database.SqlQuery<string>(
                $"select Status as Value from FocusPins where FocusPinId = {pin.FocusPinId}")));
    }

    [Fact]
    public async Task A_pin_naming_a_movement_that_does_not_exist_is_rejected()
    {
        var orphan = FocusPin.Pin(
            Guid.CreateVersion7(), _athletes.OwnerId, Guid.NewGuid(), new FocusRole.Emphasis(), Terms(new FocusTarget.None()), PinnedAt);

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() =>
            _athletes.PlantAsync(context => context.FocusPins.Add(orphan)));
        Assert.Contains("FOREIGN KEY", Assert.IsType<SqliteException>(refused.InnerException).Message);
    }

    [Fact]
    public async Task A_movement_with_a_pin_on_it_cannot_be_deleted_from_under_it()
    {
        await _athletes.PlantAsync(context => context.FocusPins.Add(FocusPin.Pin(
            Guid.CreateVersion7(), _athletes.OwnerId, _dip, new FocusRole.Emphasis(), Terms(new FocusTarget.None()), PinnedAt)));

        await Assert.ThrowsAsync<DbUpdateException>(() => _athletes.PlantAsync(context =>
            context.MovementDatas.Remove(context.MovementDatas.Single(m => m.MovementDataID == _dip))));
    }

    [Fact]
    public async Task Deleting_the_athlete_takes_their_pins_with_them()
    {
        await _athletes.PlantAsync(context => context.FocusPins.Add(FocusPin.Pin(
            Guid.CreateVersion7(), _athletes.OwnerId, _dip, new FocusRole.Emphasis(), Terms(new FocusTarget.None()), PinnedAt)));

        await _athletes.PlantAsync(context =>
            context.LionheartUsers.Remove(context.LionheartUsers.Single(u => u.UserID == _athletes.OwnerId)));

        using var fresh = _athletes.CreateContext();
        Assert.Empty(fresh.FocusPins);
    }

    [Fact]
    public async Task A_priority_round_trips_and_is_stored_as_this_json()
    {
        var pin = FocusPin.Pin(
            Guid.CreateVersion7(), _athletes.OwnerId, _dip,
            ResultAssert.Succeeded(FocusRole.Priority.For(Model.Training.Patterns.PatternFamily.Push)),
            Terms(new FocusTarget.None()), PinnedAt);
        await _athletes.PlantAsync(context => context.FocusPins.Add(pin));

        Assert.Equal(pin.Role, (await LoadAsync(pin.FocusPinId)).Role);
        Assert.Equal(
            """{"$type":"priority","Family":"push"}""",
            await StoredAsync(context => context.Database.SqlQuery<string>(
                $"select Role as Value from FocusPins where FocusPinId = {pin.FocusPinId}")));
    }

    [Fact]
    public async Task A_classification_the_athlete_stated_round_trips_and_is_stored_as_this_json()
    {
        var classification = ResultAssert.Succeeded(Model.Training.Patterns.MovementClassification.Of(
            Model.Training.Patterns.MovementPattern.HorizontalPush,
            [Model.Training.Patterns.MuscleGroup.Chest, Model.Training.Patterns.MuscleGroup.Triceps],
            [Model.Training.Patterns.MuscleGroup.FrontDelts]));
        Guid baseId;
        using (var context = _athletes.CreateContext())
        {
            var movementBase = await context.MovementDatas.Where(m => m.MovementDataID == _dip).Select(m => m.MovementBase).SingleAsync();
            movementBase.State(classification);
            baseId = movementBase.MovementBaseID;
            await context.SaveChangesAsync();
        }

        using var fresh = _athletes.CreateContext();
        var stated = Assert.IsType<Model.Training.Patterns.StatedClassification.Stated>(
            (await fresh.MovementBases.SingleAsync(mb => mb.MovementBaseID == baseId)).Classification);
        Assert.Equal(classification, stated.Classification);
        Assert.Equal(
            """{"$type":"stated","Classification":{"Pattern":"horizontalPush","Primary":["chest","triceps"],"Secondary":["frontDelts"]}}""",
            await StoredAsync(context => context.Database.SqlQuery<string>(
                $"select Classification as Value from MovementBases where MovementBaseID = {baseId}")));
    }

    private static FocusTerms Terms(FocusTarget target) => ResultAssert.Succeeded(
        FocusHorizon.Of(new DateOnly(2026, 11, 2), new DateOnly(2027, 1, 31))
            .Bind(horizon => FocusTerms.Of(horizon, 2, target)));

    private async Task<FocusPin> LoadAsync(Guid focusPinId)
    {
        using var context = _athletes.CreateContext();
        return await context.FocusPins.SingleAsync(pin => pin.FocusPinId == focusPinId);
    }

    private async Task<string> StoredAsync(Func<lionheart.Data.ModelContext, IQueryable<string>> column)
    {
        using var context = _athletes.CreateContext();
        return await column(context).SingleAsync();
    }
}
