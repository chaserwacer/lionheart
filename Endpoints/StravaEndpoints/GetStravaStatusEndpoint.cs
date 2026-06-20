using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Strava;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.StravaEndpoints
{
    [ValidateModel]
    public class GetStravaStatusEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<StravaStatusDto>
    {
        private readonly IStravaService _stravaService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetStravaStatusEndpoint(IStravaService stravaService, UserManager<IdentityUser> userManager)
        {
            _stravaService = stravaService;
            _userManager = userManager;
        }

        [HttpGet("api/strava/status")]
        [EndpointDescription("Get the Strava connection status for the authenticated user.")]
        [ProducesResponseType<StravaStatusDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<StravaStatusDto>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _stravaService.GetStatusAsync(user));
        }
    }
}
