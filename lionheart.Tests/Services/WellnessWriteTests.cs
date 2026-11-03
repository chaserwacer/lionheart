using lionheart.Model.Profile;
using lionheart.Services;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Services;

/// <summary>
/// One wellness state per athlete per day. Logging a day twice corrects the day rather than
/// adding a second row — and the write says which of the two it did, because the route answers
/// 201 for a state that did not exist and 200 for one it overwrote.
/// </summary>
public class WellnessWriteTests : IDisposable
{
    private readonly RemodelDatabase _database = new();
    private readonly IdentityUser _user;

    public WellnessWriteTests() =>
        _user = new IdentityUser("athlete@lionheart.local") { Id = _database.UserId.ToString() };

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task ADayNotLoggedYetIsCreated()
    {
        var written = await AddAsync(Request(mood: 4));

        var created = Assert.IsType<WellnessStateWrite.Created>(written);
        Assert.Equal(4, created.State.MoodScore);

        using var context = _database.CreateContext();
        Assert.Single(await context.WellnessStates.ToListAsync());
    }

    [Fact]
    public async Task LoggingTheSameDayAgainOverwritesTheOneThatIsThere()
    {
        var first = Assert.IsType<WellnessStateWrite.Created>(await AddAsync(Request(mood: 2)));
        var second = await AddAsync(Request(mood: 5));

        var updated = Assert.IsType<WellnessStateWrite.Updated>(second);
        Assert.Equal(first.State.StateID, updated.State.StateID);
        Assert.Equal(5, updated.State.MoodScore);

        using var context = _database.CreateContext();
        var stored = Assert.Single(await context.WellnessStates.ToListAsync());
        Assert.Equal(5, stored.MoodScore);
    }

    /// <summary>
    /// The overall score is one rule, so overwriting a day has to produce exactly what logging
    /// that day fresh would have.
    /// </summary>
    [Fact]
    public async Task AnOverwrittenDayScoresTheSameAsOneLoggedFresh()
    {
        await AddAsync(Request(mood: 1, energy: 1, motivation: 1, stress: 5));
        var overwritten = await AddAsync(Request(mood: 5, energy: 4, motivation: 3, stress: 2));

        var freshOnAnotherDay = await AddAsync(
            Request(mood: 5, energy: 4, motivation: 3, stress: 2, day: 19));

        Assert.Equal(freshOnAnotherDay.State.OverallScore, overwritten.State.OverallScore);
    }

    /// <summary>
    /// The volatile tier of the athlete's context card is derived from wellness, so a write has
    /// to mark it stale or the card keeps answering with yesterday.
    /// </summary>
    [Fact]
    public async Task AWriteMarksTheAthleteContextCardStale()
    {
        using (var seed = _database.CreateContext())
        {
            seed.AthleteContextCards.Add(new AthleteContextCard
            {
                CardID = Guid.NewGuid(),
                UserID = _database.UserId,
                CreatedAt = Some.Clock,
                RecentStateAsOf = Some.Clock
            });
            await seed.SaveChangesAsync();
        }

        await AddAsync(Request(mood: 3));

        using var context = _database.CreateContext();
        var card = Assert.Single(await context.AthleteContextCards.ToListAsync());
        Assert.Equal(DateTime.MinValue, card.RecentStateAsOf);
    }

    private async Task<WellnessStateWrite> AddAsync(CreateWellnessStateRequest request)
    {
        using var context = _database.CreateContext();
        return ResultAssert.Succeeded(await new WellnessService(context).AddWellnessStateAsync(_user, request));
    }

    private static CreateWellnessStateRequest Request(
        int mood = 3, int energy = 3, int motivation = 3, int stress = 3, int day = 18) =>
        new()
        {
            Date = new DateTime(2026, 7, day, 0, 0, 0, DateTimeKind.Utc),
            Mood = mood,
            Energy = energy,
            Motivation = motivation,
            Stress = stress
        };
}
