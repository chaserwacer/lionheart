using lionheart.Functional;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.Patterns;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Api;

/// <summary>
/// The athlete classifying his own movements: a statement replaces the map's entry and reaches the
/// volume, withdrawing it hands the base back to the map, and another athlete's base is missing.
/// </summary>
public class MovementClassificationServiceTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly ClassifyMovementRequest PushAccessory =
        new(MovementPattern.Isolation, [MuscleGroup.Triceps], [MuscleGroup.Chest]);

    private readonly TwoAthletes _athletes = new();

    public void Dispose() => _athletes.Dispose();

    [Fact]
    public async Task A_stated_classification_replaces_the_maps_entry_and_is_said_to_be_the_athletes()
    {
        var press = await BaseOfAsync(await _athletes.PlantMovementAsync(_athletes.OwnerId, "Tricep Press"));

        var classified = ResultAssert.Succeeded(await _athletes.ClassificationAsync(service =>
            service.ClassifyAsync(_athletes.Owner, press, PushAccessory with { Pattern = MovementPattern.HorizontalPush }, default)));

        var standing = Assert.IsType<ClassificationStanding.Classified>(classified.Classification);
        Assert.Equal(MovementPattern.HorizontalPush, standing.Classification.Pattern);
        Assert.IsType<ClassificationSource.Athlete>(standing.Source);
        Assert.Equal(classified, (await StoredAsync(press)).ToDTO());
    }

    [Fact]
    public async Task Withdrawing_hands_the_base_back_to_the_map()
    {
        var press = await BaseOfAsync(await _athletes.PlantMovementAsync(_athletes.OwnerId, "Tricep Press"));
        ResultAssert.Succeeded(await _athletes.ClassificationAsync(service =>
            service.ClassifyAsync(_athletes.Owner, press, PushAccessory with { Pattern = MovementPattern.HorizontalPush }, default)));

        var withdrawn = ResultAssert.Succeeded(await _athletes.ClassificationAsync(service =>
            service.WithdrawAsync(_athletes.Owner, press, default)));

        var standing = Assert.IsType<ClassificationStanding.Classified>(withdrawn.Classification);
        Assert.Equal(MovementPattern.Isolation, standing.Classification.Pattern);
        Assert.IsType<ClassificationSource.MapUnsure>(standing.Source);
    }

    [Fact]
    public async Task A_malformed_classification_is_refused_and_writes_nothing()
    {
        var press = await BaseOfAsync(await _athletes.PlantMovementAsync(_athletes.OwnerId, "Tricep Press"));

        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await _athletes.ClassificationAsync(service =>
            service.ClassifyAsync(_athletes.Owner, press, PushAccessory with { Primary = [] }, default))));

        Assert.Equal("Primary", Assert.Single(refusal.Fields).Field);
        Assert.IsType<StatedClassification.Unstated>((await StoredAsync(press)).Classification);
    }

    [Fact]
    public async Task Another_athletes_base_is_missing_on_both_routes()
    {
        var theirs = await BaseOfAsync(await _athletes.PlantMovementAsync(_athletes.StrangerId, "Tricep Press"));

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.ClassificationAsync(service =>
            service.ClassifyAsync(_athletes.Owner, theirs, PushAccessory, default))));
        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(await _athletes.ClassificationAsync(service =>
            service.WithdrawAsync(_athletes.Owner, theirs, default))));
        Assert.IsType<StatedClassification.Unstated>((await StoredAsync(theirs)).Classification);
    }

    [Fact]
    public async Task The_weeks_volume_counts_a_movement_by_the_athletes_classification()
    {
        var mystery = await _athletes.PlantMovementAsync(_athletes.OwnerId, "Cable Mystery");
        await _athletes.PlantAsync(context => context.Sessions.Add(TrainingHistory.Lifts(
            _athletes.OwnerId, new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.FromHours(-5)), mystery, (50, 10), (50, 10))));

        var mysteryBase = await BaseOfAsync(mystery);

        var before = ResultAssert.Succeeded(await _athletes.VolumeAsync(service => service.GetVolumeAsync(_athletes.Owner, Monday, default)));
        ResultAssert.Succeeded(await _athletes.ClassificationAsync(service =>
            service.ClassifyAsync(_athletes.Owner, mysteryBase, PushAccessory, default)));
        var after = ResultAssert.Succeeded(await _athletes.VolumeAsync(service => service.GetVolumeAsync(_athletes.Owner, Monday, default)));

        Assert.Equal(2, Assert.Single(before.Unplaced).HardSets);
        Assert.Empty(after.Unplaced);
        Assert.Equal(2, after.Families.Single(family => family.Family == PatternFamily.Push).HardSets);
        Assert.Equal(1.0, after.Muscles.Single(muscle => muscle.Muscle == MuscleGroup.Chest).Sets);
    }

    private async Task<Guid> BaseOfAsync(Guid movementDataId)
    {
        using var context = _athletes.CreateContext();
        return await context.MovementDatas
            .Where(movement => movement.MovementDataID == movementDataId)
            .Select(movement => movement.MovementBaseID)
            .SingleAsync();
    }

    private async Task<MovementBase> StoredAsync(Guid movementBaseId)
    {
        using var context = _athletes.CreateContext();
        return await context.MovementBases.AsNoTracking().SingleAsync(movementBase => movementBase.MovementBaseID == movementBaseId);
    }
}
