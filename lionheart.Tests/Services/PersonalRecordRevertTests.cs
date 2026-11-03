using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Model.Training.Sets;
using lionheart.Services;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// Reverting steps a personal record back one link in its own chain. Asking to revert one that
/// has already been reverted collides with the state it is in — the same request was legitimate
/// a moment earlier — which is a conflict, not a malformed ask and not a defect.
/// </summary>
public class PersonalRecordRevertTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly IdentityUser _user;
    private readonly Guid _movementDataId = Guid.NewGuid();

    public PersonalRecordRevertTests()
    {
        _user = new IdentityUser("athlete@lionheart.local") { Id = _database.UserId.ToString() };

        using var context = _database.CreateContext();
        var equipment = new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = "Barbell",
            UserID = _database.UserId
        };
        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = "Bench Press",
            UserID = _database.UserId,
            Description = string.Empty,
            MuscleGroups = []
        };
        context.MovementDatas.Add(new MovementData
        {
            MovementDataID = _movementDataId,
            UserID = _database.UserId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase
        });
        context.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task RevertingRestoresThePreviousRecord()
    {
        var (previous, current) = await SeedChainAsync();

        var reverted = await RevertAsync(current);

        Assert.NotNull(reverted);
        Assert.Equal(previous, reverted.PersonalRecordID);

        using var context = _database.CreateContext();
        Assert.False((await context.PersonalRecords.FindAsync(current))!.IsActive);
        Assert.True((await context.PersonalRecords.FindAsync(previous))!.IsActive);
    }

    /// <summary>The first record of its kind has nothing behind it to restore.</summary>
    [Fact]
    public async Task RevertingTheOnlyRecordLeavesNothingActive()
    {
        var only = await SeedRecordAsync(weight: 225, isActive: true);

        Assert.Null(await RevertAsync(only));

        using var context = _database.CreateContext();
        Assert.False((await context.PersonalRecords.FindAsync(only))!.IsActive);
    }

    [Fact]
    public async Task RevertingAnAlreadyRevertedRecordIsAConflict()
    {
        var inactive = await SeedRecordAsync(weight: 225, isActive: false);

        Assert.IsType<Problem.Conflict>(await RefusalAsync(inactive));
    }

    [Fact]
    public async Task ARecordThatIsNotTheAthletesIsNotFound() =>
        Assert.IsType<Problem.NotFound>(await RefusalAsync(Guid.NewGuid()));

    private async Task<(Guid Previous, Guid Current)> SeedChainAsync()
    {
        var previous = await SeedRecordAsync(weight: 225, isActive: false);
        var current = await SeedRecordAsync(weight: 245, isActive: true, previousRecordId: previous);
        return (previous, current);
    }

    private async Task<Guid> SeedRecordAsync(double weight, bool isActive, Guid? previousRecordId = null)
    {
        using var context = _database.CreateContext();
        var record = new PersonalRecord
        {
            PersonalRecordID = Guid.NewGuid(),
            UserID = _database.UserId,
            MovementDataID = _movementDataId,
            MovementData = (await context.MovementDatas.FindAsync(_movementDataId))!,
            PRType = PersonalRecordType.Strength,
            Weight = weight,
            WeightUnit = WeightUnit.Pounds,
            Reps = 5,
            CreatedAt = Some.Clock,
            IsActive = isActive,
            PreviousPersonalRecordID = previousRecordId
        };

        context.PersonalRecords.Add(record);
        await context.SaveChangesAsync();
        return record.PersonalRecordID;
    }

    private async Task<PersonalRecordDTO?> RevertAsync(Guid personalRecordId)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Succeeded(
            await new PersonalRecordService(context).RevertToPreviousAsync(_user, personalRecordId));
    }

    private async Task<Problem> RefusalAsync(Guid personalRecordId)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Failed(
            await new PersonalRecordService(context).RevertToPreviousAsync(_user, personalRecordId));
    }
}
