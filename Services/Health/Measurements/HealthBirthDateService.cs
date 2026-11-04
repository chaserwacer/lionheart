using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Health;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health.Profile;

public interface IHealthBirthDateService
{
    Task<Result<ProfileAgeView, Problem>> ApplyAsync(IdentityUser user, HealthBirthDate birthDate, CancellationToken ct);
}

/// <summary>Sets the profile's age from Health's date of birth when it is unset or has gone stale.</summary>
/// <remarks>
/// Health's date wins over a typed age: it is the athlete's own entry in Health, and an age that
/// differs from it is one a birthday has passed. The day is the server's, as every other "today"
/// here is; a day's disagreement with the phone's clock around midnight on a birthday settles at
/// the next sync.
/// </remarks>
public sealed class HealthBirthDateService : IHealthBirthDateService
{
    private readonly ModelContext _context;
    private readonly TimeProvider _clock;

    public HealthBirthDateService(ModelContext context, TimeProvider clock)
    {
        _context = context;
        _clock = clock;
    }

    public Task<Result<ProfileAgeView, Problem>> ApplyAsync(IdentityUser user, HealthBirthDate birthDate, CancellationToken ct) =>
        _context.AthleteIdAsync(user, ct)
            .BindAsync(userId => birthDate.AgeOn(DateOnly.FromDateTime(_clock.GetLocalNow().DateTime)).Map(age => (userId, age)))
            .MapAsync(aged => AppliedAsync(aged.userId, aged.age, ct));

    private async Task<ProfileAgeView> AppliedAsync(Guid userId, int age, CancellationToken ct)
    {
        var athlete = await _context.LionheartUsers.FirstAsync(athlete => athlete.UserID == userId, ct);
        if (athlete.Age == age) return new ProfileAgeView(age, Changed: false);

        athlete.Age = age;
        await _context.SaveChangesAsync(ct);
        return new ProfileAgeView(age, Changed: true);
    }
}
