using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.UserEndpoints
{
    /// <summary>
    /// Answers whether the signed-in identity has a Lionheart profile yet.
    /// </summary>
    /// <remarks>
    /// The client needs this on boot: every training write has a foreign key to the profile, so a
    /// registered identity without one cannot log anything. Without this route the app could only
    /// infer profile state from a failed write, which is why its earlier removal left the frontend
    /// bouncing every page load to the auth screen.
    /// </remarks>
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
        [EndpointDescription("Whether the signed-in user has created a Lionheart profile.")]
        [ProducesResponseType<BootUserDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<BootUserDTO>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _userService.HasCreatedProfileAsync(user));
        }
    }
}
