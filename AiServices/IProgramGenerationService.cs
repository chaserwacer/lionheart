using Ardalis.Result;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace lionheart.Services.AI
{
    public interface IProgramGenerationService
    {

        Task<Result<string>> GeneratePreferencesAsync(IdentityUser user, ProgramPreferencesDTO dto);
        Task<Result<string>> GenerateFirstWeekAsync(IdentityUser user, FirstWeekGenerationDTO dto);
        Task<Result<string>> GenerateRemainingWeeksAsync(IdentityUser user, RemainingWeeksGenerationDTO dto);
    }
}
