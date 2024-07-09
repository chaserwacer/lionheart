using lionheart.Services;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;


namespace lionheart.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;


        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }


        [HttpGet("/api/[controller]/[action]")]
        public string? GetName()
        {
            return this.User.Identity?.Name ?? "Not Authorized";
            //User.Identity.
            //User identity name os the key
            //System.Security.Claims.ClaimsPrincipal currentUser = this.User;
            //var ID = User.FindFirstValue(ClaimTypes.NameIdentifier)
            // var name = User.FindFirstValue(ClaimTypes.Name);
            // return name is null ? Ok("null") : (IActionResult)Ok(name);
        }

        [HttpPost("[action]")]
        public async Task<LionheartUserDto> CreateProfile(CreateProfileRequest req){
            var lionheartUser = _userService.CreateProfile(req.User, User.Identity.Name); // returnn my obj
            // create dto via getting user and sending back dto logic
            // static method on dto, take lionheart user and return dto
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
}
