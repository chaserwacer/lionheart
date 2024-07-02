using lionheart.Services;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(Guid userId)
        {
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            return Ok(user.UserID);
        }

        [HttpGet("details/{userId}")]
        public async Task<IActionResult> GetUserDetails(Guid userId)
        {
            var user = await _userService.GetUserAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var userDetails = new
            {
                user.Age,
                user.Weight
            };

            return Ok(userDetails);
        }

        [HttpPost("signin")]
        public async Task<IActionResult> SignIn([FromBody] UserDto userDto)
        {
            var user = await _userService.GetUserAsync(userDto.UserID);
            if (user == null)
            {
                user = await _userService.CreateUserAsync(new User(userDto.UserID, userDto.Name, age: 0, weight: 0));
            }

            return Ok(user.UserID);
        }



        // [HttpPost]
        // public async Task<IActionResult> SignUpUser([FromBody] User user)
        // {
        //     Console.WriteLine($"Received the user object: {JsonSerializer.Serialize(user)}");
        //     _logger.LogDebug("Received user object: {User}", JsonSerializer.Serialize(user));
        //     var createdUser = await _userService.CreateUserAsync(user);
        //     Console.WriteLine($"Created and rerecieved the user object: {JsonSerializer.Serialize(createdUser)}");
        //     return CreatedAtAction(nameof(GetUser), new { userId = createdUser.UserID }, createdUser);
        // }

        // [HttpPost("login")]
        // public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
        // {
        //     var user = await _userService.LoginAsync(loginDto.Username, loginDto.Password);
        //     if (user == null)
        //         return Unauthorized();
        //     return Ok(user);
        // }

        // [HttpGet("{userId}")]
        // public async Task<IActionResult> GetUser(Guid userId)
        // {
        //     var user = await _userService.GetUserAsync(userId);
        //     if (user == null) return NotFound();
        //     return Ok(user);
        // }

        [HttpPost("{userId}/wellness")]
        public async Task<IActionResult> AddWellnessState(Guid userId, [FromBody] WellnessState wellnessState)
        {
            try
            {
                await _userService.AddWellnessStateAsync(userId, wellnessState);
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("{userId}/wellness")]
        public async Task<IActionResult> GetWellnessStates(Guid userId)
        {
            try
            {
                var wellnessStates = await _userService.GetWellnessStatesAsync(userId);
                return Ok(wellnessStates);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet]
        [Route("/api/[controller]/[action]")]
        public string GetMessage()
        {
            return "hiii";
        }


    }
}

public class UserDto{
    public required Guid UserID { get; set; }
    public required string Name { get; set; }
}
