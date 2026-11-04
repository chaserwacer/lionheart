using lionheart.Data;
using lionheart.Model.Interpretation;
using lionheart.Model.Training;
using lionheart.Model.User;
using lionheart.Services;
using lionheart.Services.Interpretation;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Services.Training.Patterns;

namespace lionheart.Tests.Interpretation;

public class MovementResolverTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly IdentityUser _user;

    public MovementResolverTests() =>
        _user = new IdentityUser("resolver@lionheart.local") { Id = _database.UserId.ToString() };

    public void Dispose() => _database.Dispose();

    private async Task SeedAsync(string[] bases, string[] equipment)
    {
        using var context = _database.CreateContext();
        foreach (var name in bases)
        {
            context.MovementBases.Add(new MovementBase
            {
                MovementBaseID = Guid.NewGuid(),
                Name = name,
                UserID = _database.UserId,
                Description = string.Empty
            });
        }

        foreach (var name in equipment)
        {
            context.Equipments.Add(new Equipment
            {
                EquipmentID = Guid.NewGuid(),
                Name = name,
                UserID = _database.UserId
            });
        }

        await context.SaveChangesAsync();
    }

    private static MovementResolver BuildResolver(ModelContext context) =>
        new(context, new MovementDataService(context, new MovementModifierService(context), MovementPatternMap.Shipped));

    [Fact]
    public async Task ExactNamesResolveToAMovementDataId()
    {
        await SeedAsync(["Bench Press"], ["Barbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(
            _user, [new MovementReference("Bench Press", "Barbell", string.Empty)]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Empty(resolved.Unresolved);
        Assert.Single(resolved.Resolved);
        Assert.NotEqual(Guid.Empty, resolved.Resolved.Values.Single());
    }

    [Fact]
    public async Task MatchingIgnoresCaseAndSurroundingWhitespace()
    {
        await SeedAsync(["Bench Press"], ["Barbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(
            _user, [new MovementReference("  bench press ", "BARBELL", string.Empty)]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Empty(resolved.Unresolved);
    }

    [Fact]
    public async Task UnknownMovementComesBackWithRankedCandidates()
    {
        await SeedAsync(["Front Squat", "Bench Press"], ["Barbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(
            _user, [new MovementReference("Squat", "Barbell", string.Empty)]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Empty(resolved.Resolved);

        var unresolved = Assert.Single(resolved.Unresolved);
        Assert.Equal("Squat", unresolved.RawName);
        Assert.Equal(ReferenceKind.MovementBase, unresolved.Kind);
        Assert.Equal("Front Squat", unresolved.Candidates.First().Name);
    }

    [Fact]
    public async Task UnknownEquipmentIsReportedSeparatelyFromTheMovement()
    {
        await SeedAsync(["Bench Press"], ["Dumbbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(
            _user, [new MovementReference("Bench Press", "Barbell", string.Empty)]);

        var resolved = ResultAssert.Succeeded(resolution);
        var unresolved = Assert.Single(resolved.Unresolved);
        Assert.Equal(ReferenceKind.Equipment, unresolved.Kind);
        Assert.Equal("Barbell", unresolved.RawName);
    }

    /// <summary>The same movement across many sets must not produce a wall of identical prompts.</summary>
    [Fact]
    public async Task RepeatedUnknownNamesAreReportedOnce()
    {
        await SeedAsync(["Bench Press"], ["Barbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(_user,
        [
            new MovementReference("Sled Pull", "Barbell", string.Empty),
            new MovementReference("Sled Pull", "Barbell", string.Empty),
            new MovementReference("sled pull", "Barbell", string.Empty)
        ]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Single(resolved.Unresolved);
    }

    /// <summary>
    /// FindOrCreate persists, so a run the user will be asked to disambiguate must not create
    /// MovementData for the references that happened to match.
    /// </summary>
    [Fact]
    public async Task NothingIsCreatedWhenAnyReferenceIsAmbiguous()
    {
        await SeedAsync(["Bench Press"], ["Barbell"]);

        using (var context = _database.CreateContext())
        {
            var resolution = await BuildResolver(context).ResolveAsync(_user,
            [
                new MovementReference("Bench Press", "Barbell", string.Empty),
                new MovementReference("Sled Pull", "Barbell", string.Empty)
            ]);

            var resolved = ResultAssert.Succeeded(resolution);
            Assert.NotEmpty(resolved.Unresolved);
            Assert.Empty(resolved.Resolved);
        }

        using var verify = _database.CreateContext();
        Assert.Empty(await verify.MovementDatas.ToListAsync());
    }

    [Fact]
    public async Task SameMovementWithDifferentModifiersResolvesToDifferentIds()
    {
        await SeedAsync(["Bench Press"], ["Barbell"]);

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(_user,
        [
            new MovementReference("Bench Press", "Barbell", string.Empty),
            new MovementReference("Bench Press", "Barbell", "Incline")
        ]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Equal(2, resolved.Resolved.Count);
        Assert.Equal(2, resolved.Resolved.Values.Distinct().Count());
    }

    [Fact]
    public async Task MovementsBelongingToAnotherUserAreNotMatched()
    {
        using (var seed = _database.CreateContext())
        {
            var otherIdentity = new IdentityUser("other@lionheart.local");
            var otherUserId = Guid.NewGuid();
            seed.Users.Add(otherIdentity);
            seed.LionheartUsers.Add(new LionheartUser
            {
                UserID = otherUserId,
                IdentityUser = otherIdentity,
                Name = "Someone Else"
            });
            seed.MovementBases.Add(new MovementBase
            {
                MovementBaseID = Guid.NewGuid(),
                Name = "Bench Press",
                UserID = otherUserId,
                Description = string.Empty
            });
            await seed.SaveChangesAsync();
        }

        using var context = _database.CreateContext();
        var resolution = await BuildResolver(context).ResolveAsync(
            _user, [new MovementReference("Bench Press", "Barbell", string.Empty)]);

        var resolved = ResultAssert.Succeeded(resolution);
        Assert.Contains(resolved.Unresolved, u => u.Kind == ReferenceKind.MovementBase);
    }
}
