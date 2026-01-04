using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using Microsoft.AspNetCore.Authentication;

namespace lionheart.Endpoints.UserEndpoints
{
    [ValidateModel]
    public class LogoutUserEndpoint : EndpointBaseAsync
        .WithRequest<object>
        .WithActionResult
    {
        private readonly IUserService _userService;
        private readonly UserManager<IdentityUser> _userManager;

        public LogoutUserEndpoint(IUserService userService, UserManager<IdentityUser> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }

        [HttpPost("api/user/logout")]
        [EndpointDescription("Logout user from the application.")]
        [ProducesResponseType<BootUserDTO>(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult> HandleAsync([FromQuery] object request, CancellationToken cancellationToken = default)
        {
            await HttpContext.SignOutAsync("Identity.Application");
            return NoContent();
        }
    }
}