using lionheart.Data;
using lionheart.Functional;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Establishing that a signed-in identity is an athlete this application knows.
/// </summary>
internal static class AthleteProfiles
{
    extension(ModelContext context)
    {
        /// <summary>
        /// The athlete's id, or a refusal that they have no profile yet.
        /// </summary>
        /// <remarks>
        /// Registering through the Identity API creates an <see cref="IdentityUser"/> and nothing
        /// else, while sessions, programs and contexts all carry a foreign key to LionheartUser.
        /// Without this guard the first write a new account makes fails as a constraint violation
        /// escaping to a 500, which says nothing about the one thing they have to do first.
        /// </remarks>
        public async Task<Result<Guid, Problem>> AthleteIdAsync(IdentityUser user, CancellationToken ct)
        {
            var userId = Guid.Parse(user.Id);
            var profiled = await context.LionheartUsers.AnyAsync(u => u.UserID == userId, ct);

            return profiled
                ? Result<Guid, Problem>.Success(userId)
                : Result<Guid, Problem>.Failure(new Problem.Conflict(
                    "This account has no Lionheart profile yet. Create one before logging training."));
        }

        /// <summary>
        /// The athlete named by a raw id, or a refusal saying no such athlete exists.
        /// </summary>
        /// <remarks>
        /// For the command-line tools, which are given an id rather than an identity: a mistyped one
        /// should say so rather than fail later as a foreign-key violation.
        /// </remarks>
        public async Task<Result<Guid, Problem>> KnownAthleteAsync(Guid userId, CancellationToken ct) =>
            await context.LionheartUsers.AnyAsync(user => user.UserID == userId, ct)
                ? Result<Guid, Problem>.Success(userId)
                : Result<Guid, Problem>.Failure(new Problem.NotFound(
                    $"No Lionheart athlete with id {userId}, so there is nobody to attach this to."));
    }
}
