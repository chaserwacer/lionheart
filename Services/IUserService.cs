using lionheart.Controllers;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace lionheart.Services
{
    public interface IUserService
    {
        Task<WellnessState> AddWellnessStateAsync(CreateWellnessStateRequest req, string userID);
        //Task<List<WellnessState>> GetWellnessStatesAsync(string userId);
        Task<(Boolean, string)> HasCreatedProfileAsync(string? userID);
        Task<LionheartUser> CreateProfileAsync(CreateProfileRequest req, string userID);
        Task<WellnessState> GetWellnessStateAsync(string userID, DateOnly date);
        Task<List<WellnessState>> GetLastXWellnessStatesAsync(string userID, DateOnly endDate, int X);
        Task<(List<double>, List<string>)> GetLastXWellnessStatesGraphDataAsync(string userID, DateOnly date, int X);
        Task<bool> SetPersonalApiAccessToken(string userID, string applicationName, string accessToken);
    }
}
