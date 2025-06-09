using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.Oura;
using Microsoft.AspNetCore.Identity;
namespace lionheart.Services
{
    public interface IOuraService
    {
        /// <summary>
        /// Synchronize Oura data for a user within a specified date range.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="dateRange"></param>
        /// <returns></returns>
        Task<Result> SyncOuraAPI(IdentityUser user, DateRangeRequest dateRange);
        /// <summary>
        /// Get the daily Oura information for a user on a specific date.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        Task<Result<DailyOuraDataDTO>> GetDailyOuraInfoAsync(IdentityUser user, DateOnly date);
    }
}