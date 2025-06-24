using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Identity;

public interface IWellnessService
{
    /// <summary>
    /// Fetch the <see cref="WellnessState"/> for <paramref name="user"/> on a specific <paramref name="date"/>.
    /// If no wellness state exists for that date, a blank one will be returned with default values. 
    /// </summary>
    /// <param name="userID">Identity User username</param>
    /// <param name="date"></param>
    /// <returns></returns>
    Task<Result<WellnessState>> GetWellnessStateAsync(IdentityUser user, DateOnly date);

    /// <summary>
    /// Select all <see cref="WellnessState"/>s for the <paramref name="user"/> within the specified <paramref name="dateRange"/>.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="dateRange"></param>
    /// <returns></returns>
    Task<Result<List<WellnessState>>> GetWellnessStatesAsync(IdentityUser user, DateRangeRequest dateRange);

    /// <summary>
    /// Get a DTO object containing overall wellness scores and dates for the last <paramref name="X"/> days before <paramref name="date"/>.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="endDate">Last day wellness data will be retreived from</param>
    /// <param name="X">Number days prior to endDate that will have their wellness data retreived from</param>
    /// <returns></returns>
    // Task<Result<WeeklyScoreDTO, ServiceError>> GetWellnessStatesGraphDataAsync(IdentityUser user, DateOnly date, int X);

    /// <summary>
    /// Add a new <see cref="WellnessState"/> for the <paramref name="user"/>.
    /// If a wellness state already exists for the date, it will be overwritten.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="req"></param>
    /// <returns></returns>
    Task<Result<WellnessState>> AddWellnessStateAsync(IdentityUser user, CreateWellnessStateRequest req);

    Task<List<WellnessState>> GetWellnessStatesMCP(string userID, DateRangeRequest dateRange);
}

