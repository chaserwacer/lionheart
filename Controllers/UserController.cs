using lionheart.Services;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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


        [HttpGet("/api/[controller]/[action]")]
        public string? GetUsername()
        {
            // ATP this is the users email
            return User.Identity?.Name ?? "Not Authorized";
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
            catch(Exception)
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



        // [HttpPost("{userId}/wellness")]
        // public async Task<IActionResult> AddWellnessState(Guid userId, [FromBody] WellnessState wellnessState)
        // {
        //     try
        //     {
        //         await _userService.AddWellnessStateAsync(userId, wellnessState);
        //         return NoContent();
        //     }
        //     catch (InvalidOperationException ex)
        //     {
        //         return NotFound(ex.Message);
        //     }
        // }

        // [HttpGet("{userId}/wellness")]
        // public async Task<IActionResult> GetWellnessStates(Guid userId)
        // {
        //     try
        //     {
        //         var wellnessStates = await _userService.GetWellnessStatesAsync(userId);
        //         return Ok(wellnessStates);
        //     }
        //     catch (InvalidOperationException ex)
        //     {
        //         return NotFound(ex.Message);
        //     }
        // }

        [HttpGet]
        [Route("/api/[controller]/[action]")]
        public string GetMessage()
        {
            return "hiii";
        }

        
    }
    public record LRModel
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
    }
    public record BootUserDto(string? Name, Boolean HasCreatedProfile);
    public record CreateProfileRequest(string DisplayName, int Age, float Weight);
}
