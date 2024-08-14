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


        [HttpGet("[action]")]
        public async Task<BootUserDto> GetBootUserDtoAsync()
        {
            // Returns a DTO indicating the username for a user and whether or not they have created a lionheart profile
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


        [HttpPost("[action]")]
        public async Task<IActionResult> AddWellnessState(CreateWellnessStateRequest req)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error adding Wellness State - username/key was null"); }
                var state = await _userService.AddWellnessStateAsync(req, User.Identity.Name);
                return Ok(state);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to add Wellness State: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get Wellness State from given date
        /// </summary>
        [HttpGet("[action]")]
        public async Task<WellnessState> GetWellnessStateAsync(DateOnly date)
        {
            try
            {
                if (User.Identity?.Name is null) { return new WellnessState(new Guid(), 1, 1, 1, 1){ OverallScore = -1}; }
                return await _userService.GetWellnessStateAsync(User.Identity.Name, date);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get WellnessState: {e.Message}", e);
                throw;
            }
        }

        /// <summary>
        /// Get Wellness States from the past x-days coming before 'date'
        /// </summary>
        [HttpGet("[action]")]
        public async Task<List<WellnessState>> GetLastXWellnessStatesAsync(DateOnly date, int xDays)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error getting Wellness States - username/key was null"); }
                if (xDays <= 0) { throw new Exception("Call to GetLastXWellnessStatesAsync has invalid number of days"); }
                return await _userService.GetLastXWellnessStatesAsync(User.Identity.Name, date, xDays);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get WellnessState: {e.Message}", e);
                throw;
            }
        }

        [HttpGet("[action]")]
        public async Task<WeeklyScoreDTO> GetLastXWellnessStatesGraphData(DateOnly date, int xDays)
        {
            try
            {
                if (User.Identity?.Name is null) { throw new NullReferenceException("Error getting Wellness States - username/key was null"); }
                if (xDays <= 0) { throw new Exception("Call to GetLastXWellnessStatesAsync has invalid number of days"); }
                var tup = await _userService.GetLastXWellnessStatesGraphDataAsync(User.Identity.Name, date, xDays);
                return new WeeklyScoreDTO(tup.Item1, tup.Item2);
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to get WellnessState: {e.Message}", e);
                throw;
            }
        }


    }//end userController
    public record BootUserDto(string? Name, Boolean HasCreatedProfile);
    public record CreateProfileRequest(string DisplayName, int Age, float Weight);
    public record CreateWellnessStateRequest(int Energy, int Motivation, int Mood, int Stress);
    public record WeeklyScoreDTO(List<double> Scores, List<string> Dates);
}
