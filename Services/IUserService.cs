using lionheart.Controllers;
using lionheart.WellBeing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace lionheart.Services
{
    public interface IUserService
    {
        Task AddWellnessStateAsync(Guid userId, WellnessState wellnessState);
        Task<List<WellnessState>> GetWellnessStatesAsync(Guid userId);
        Task<(Boolean, string)> HasCreatedProfileAsync(string? userID);
        Task<LionheartUser> CreateProfileAsync(CreateProfileRequest req, string userID);

    }
}