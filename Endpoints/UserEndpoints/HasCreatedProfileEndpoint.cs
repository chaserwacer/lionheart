using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.UserEndpoints
{
    [ValidateModel]
    public class HasCreatedProfileEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<BootUserDTO>
    {
        private readonly IUserService _userService;
        private readonly UserManager<IdentityUser> _userManager;

        public HasCreatedProfileEndpoint(IUserService userService, UserManager<IdentityUser> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }

        [HttpGet("api/user/has-created-profile")]
        [EndpointDescription("Check the status of the user's profile creation.")]
        [ProducesResponseType<BootUserDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<BootUserDTO>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) 
            { 
                return Ok(new BootUserDTO("", false)); 
            }
            // if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _userService.HasCreatedProfileAsync(user));
        }
    }
}