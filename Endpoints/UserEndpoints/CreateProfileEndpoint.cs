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
    public class CreateProfileEndpoint : EndpointBaseAsync
        .WithRequest<CreateProfileRequest>
        .WithActionResult<LionheartUser>
    {
        private readonly IUserService _userService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateProfileEndpoint(IUserService userService, UserManager<IdentityUser> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }

        [HttpPost("api/user/create-profile")]
        [EndpointDescription("Create a new profile for the user.")]
        [ProducesResponseType<LionheartUser>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<LionheartUser>> HandleAsync([FromBody] CreateProfileRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _userService.CreateProfileAsync(user, request));
        }
    }
}