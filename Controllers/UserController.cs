using lionheart.Services;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;


namespace lionheart.Controllers
{
    /// <summary>
    /// User Controller class contains the endpoints for all user methods, calling upon the UserService for business logic. 
    /// As discussed in UserService, this class also contains some items that are not directly related to user account creation -
    ///  these are to be taken out at some point. 
    ///  
    /// For clarification on the state of a user:
    ///  A user can either be nonexistant, registered, or fully created. Registered means that an email and password have been used to create an
    ///  ASP.Net Identity User (authentication), but have not yet created a LionheartUser profile. Once that LionheartUser profile is created, they are 
    ///  then a full user. A lionheart user exists to hold other custom info not contained within the ASP.NET Identity User class. They are associated with 
    ///  one-another in the database. See modelContext for more info. 
    ///  When a user logs in (regardless of whether or not they have a full profile), their Identity User username is stored inside cookies, which we can 
    ///  access as such: User.Identity.Name
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger, HttpClient httpClient)
        {
            _userService = userService;
            _logger = logger;
        }


        /// <summary>
        /// Attempt to create Lionheart Profile for identity user. This method is run assuming that an Identity User exists
        /// </summary>
        /// <param name="req">Obj holding data to be stored in Lionheart User</param>
        /// <returns>Boot user with display name and success of profile creation</returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> CreateProfileAsync(CreateProfileRequest req)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error creating Lionheart profile - username/key was null"); }
                var lionheartUser = await _userService.CreateProfileAsync(req, User.Identity.Name);
                return Ok(lionheartUser);
            }
            catch (Exception)
            {
                _logger.LogError("Error during creation of Lionheart profile");
                throw;
            }
        }


        /// <summary>
        /// Returns a DTO indicating the username/display name for a user and whether or not they have created a lionheart profile
        /// </summary>
        /// <returns></returns>
        [HttpGet("[action]")]
        public async Task<BootUserDto> GetBootUserDtoAsync()
        {
            try
            {
                string? userName = User?.Identity?.Name;
                var hasCreatedProfile = await _userService.HasCreatedProfileAsync(userName);
                return new BootUserDto(hasCreatedProfile.Item2, hasCreatedProfile.Item1);
            }
            catch (Exception)
            {
                _logger.LogError("Failed to get BootUserDto");
                throw;
            }

        }

        /// <summary>
        /// Logs out a user from the application via clearing the http cookies that hold the users 'Identity User' username.
        /// </summary>
        /// <returns></returns>
        [HttpPost("[action]")]
        public async Task<IActionResult> LogoutUserAsync()
        {
            try
            {
                await HttpContext.SignOutAsync("Identity.Application");
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError($"{e.Message}", e);
                throw;
            }
        }


        /// <summary>
        /// Adds a wellness state for a given user.
        /// </summary>
        /// <param name="req">DTO object containing necessary date for wellness state creation</param>
        /// <returns>IActionResult of Wellness State Addition</returns>
        // [HttpPost("[action]")]
        // public async Task<IActionResult> AddWellnessState(CreateWellnessStateRequest req)
        // {
        //     try
        //     {
        //         if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
        //         var state = await _userService.AddWellnessStateAsync(req, User.Identity.Name);
        //         return Ok(state);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"Failed to add Wellness State: {e.Message}", e);
        //         throw;
        //     }
        // }

        /// <summary>
        /// Get wellness state for given date
        /// </summary>
        /// <param name="date"></param>
        /// <returns>wellness state</returns>
        // [HttpGet("[action]")]
        // public async Task<WellnessState> GetWellnessStateAsync(DateOnly date)
        // {
        //     try
        //     {
        //         if (User.Identity?.Name is null) { return new WellnessState(new Guid(), 1, 1, 1, 1, date){ OverallScore = -1}; }
        //         return await _userService.GetWellnessStateAsync(User.Identity.Name, date);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"Failed to get WellnessState: {e.Message}", e);
        //         throw;
        //     }
        // }

        /// <summary>
        /// Get Wellness States from the past x-days coming before 'date'
        /// </summary>
        /// <param name="date"></param>
        /// <param name="xDays"></param>
        /// <returns>List<WellnessState></returns>
        // [HttpGet("[action]")]
        // public async Task<List<WellnessState>> GetLastXWellnessStatesAsync(DateOnly date, int xDays)
        // {
        //     try
        //     {
        //         if (User.Identity?.Name is null) { throw new NullReferenceException("Error getting Wellness States - username/key was null"); }
        //         if (xDays <= 0) { throw new Exception("Call to GetLastXWellnessStatesAsync has invalid number of days"); }
        //         return await _userService.GetLastXWellnessStatesAsync(User.Identity.Name, date, xDays);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"Failed to get WellnessStates: {e.Message}", e);
        //         throw;
        //     }
        // }

        // /// <summary>
        // /// Get DTO object containing (overall Wellness score, date) for the past xDays before date
        // /// </summary>
        // /// <param name="date"></param>
        // /// <param name="xDays"></param>
        // /// <returns></returns>
        // [HttpGet("[action]")]
        // public async Task<WeeklyScoreDTO> GetLastXWellnessStatesGraphData(DateOnly date, int xDays)
        // {
        //     try
        //     {
        //         if (User.Identity?.Name is null) { throw new NullReferenceException("Error getting Wellness States - username/key was null"); }
        //         if (xDays <= 0) { throw new Exception("Call to GetLastXWellnessStatesAsync has invalid number of days"); }
        //         var tup = await _userService.GetLastXWellnessStatesGraphDataAsync(User.Identity.Name, date, xDays);
        //         return new WeeklyScoreDTO(tup.Item1, tup.Item2);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"Failed to get WellnessStates: {e.Message}", e);
        //         throw;
        //     }
        // }

        // /// <summary>
        // /// Add a personal api access token for a given application
        // /// </summary>
        // /// <param name="applicationName"></param>
        // /// <param name="accessToken"></param>
        // /// <returns>Result of token adding</returns>
        // [HttpPost("[action]")]
        // public async Task<IActionResult> AddPersonalApiAccessToken(string applicationName, string accessToken)
        // {
        //     try
        //     {
        //         if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
        //         var res = await _userService.SetPersonalApiAccessToken(User.Identity.Name, applicationName, accessToken);
        //         return Ok(res);
        //     }
        //     catch (Exception e)
        //     {
        //         _logger.LogError($"Failed to add personal api access token: {e.Message}", e);
        //         throw;
        //     }
        // }


    }//end userController

    /// <summary>
    /// Dto for handling a user and their state of profile creation
    /// </summary>
    /// <param name="Name">username/display name</param>
    /// <param name="HasCreatedProfile">boolean indicating profile creation state</param>
    public record BootUserDto(string? Name, Boolean HasCreatedProfile);

    /// <summary>
    /// DTO object handling creation of LionheartUsers
    /// </summary>
    /// <param name="DisplayName"></param>
    /// <param name="Age"></param>
    /// <param name="Weight"></param>
    public record CreateProfileRequest(string DisplayName, int Age, float Weight);
    /// <summary>
    /// DTO object handling creation of WellnessStates
    /// </summary>
    /// <param name="Date"></param>
    /// <param name="Energy"></param>
    /// <param name="Motivation"></param>
    /// <param name="Mood"></param>
    /// <param name="Stress"></param>
    public record CreateWellnessStateRequest(string Date, int Energy, int Motivation, int Mood, int Stress);
    /// <summary>
    /// DTO handling objects used for a graph on the front-end. 
    /// </summary>
    /// <param name="Scores">List of OverallScores for a number of wellness states</param>
    /// <param name="Dates">List of dates for these wellness states</param>
    public record WeeklyScoreDTO(List<double> Scores, List<string> Dates);
}
