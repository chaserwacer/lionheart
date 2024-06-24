using lionheart.Services;
using lionheart.WellBeing;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace lionheart.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            var createdUser = await _userService.CreateUserAsync(user);
            return CreatedAtAction(nameof(GetUser), new { userId = createdUser.UserID }, createdUser);
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUser(Guid userId)
        {
            var user = await _userService.GetUserAsync(userId);
            if (user == null) return NotFound();
            return Ok(user);
        }

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
    }
}
