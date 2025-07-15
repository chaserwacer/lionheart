using Ardalis.Result;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.AI
{
    public interface IProgramGenerationService
    {
        Task<Result<string>> GenerateInitializationAsync(IdentityUser user);
        Task<Result<string>> GenerateProgramShellAsync(IdentityUser user, Dictionary<string, object>? inputs);
        Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, Dictionary<string, object>? inputs);
        Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, Dictionary<string, object>? inputs);
        Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, Dictionary<string, object>? inputs);
    }
}
