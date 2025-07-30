using Ardalis.Result;
using lionheart.Model.Injury;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services
{
    public interface IInjuryService
    {
        Task<Result<InjuryDTO>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request);
        Task<Result<InjuryDTO>> AddInjuryEventAsync(IdentityUser user, Guid injuryId, CreateInjuryEventRequest request);
        Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user);
        Task<Result> MarkInjuryResolvedAsync(IdentityUser user, Guid injuryId);
    }
}
