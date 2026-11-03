using Ardalis.ApiEndpoints;
using lionheart.Functional;
using lionheart.Services;
using lionheart.Model.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using lionheart.Endpoints.Filters;

namespace lionheart.Endpoints.UserEndpoints
{
    [ValidateModel]
    public class CreateProfileEndpoint : EndpointBaseAsync
        .WithRequest<CreateProfileRequest>
        .WithActionResult<UserProfileView>
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
        [ProducesResponseType<UserProfileView>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<UserProfileView>> HandleAsync([FromBody] CreateProfileRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            // Projected, not the entity: LionheartUser carries the Identity account and the
            // athlete's API tokens as navigation properties, and serializing it put a password
            // hash on the wire.
            return this.ToActionResult(
                (await _userService.CreateProfileAsync(user, request))
                    .Map(profile => UserProfileView.Of(profile)),
                StatusCodes.Status201Created);
        }
    }
}