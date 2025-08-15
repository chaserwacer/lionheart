using Ardalis.Result;
using lionheart.Model.Injury;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services
{
    public interface IInjuryService
    {
        Task<Result<InjuryDTO>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request);
        Task<Result<InjuryDTO>> UpdateInjuryAsync(IdentityUser user, UpdateInjuryRequest request);
        Task<Result> DeleteInjuryAsync(IdentityUser user, Guid injuryId);

        Task<Result<InjuryDTO>> CreateInjuryEventAsync(IdentityUser user,  CreateInjuryEventRequest request);
    Task<Result<InjuryDTO>> UpdateInjuryEventAsync(IdentityUser user, UpdateInjuryEventRequest request);
        Task<Result> DeleteInjuryEventAsync(IdentityUser user, Guid injuryEventId);
        Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user);
        
        
    }
}
