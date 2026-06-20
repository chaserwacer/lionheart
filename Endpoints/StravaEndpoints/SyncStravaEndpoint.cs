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
    public class SyncStravaEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<StravaSyncResultDto>
    {
        private readonly IStravaService _stravaService;
        private readonly UserManager<IdentityUser> _userManager;

        public SyncStravaEndpoint(IStravaService stravaService, UserManager<IdentityUser> userManager)
        {
            _stravaService = stravaService;
            _userManager = userManager;
        }

        [HttpPost("api/strava/sync")]
        [EndpointDescription("Sync the authenticated user's Strava activities into the lionheart database.")]
        [ProducesResponseType<StravaSyncResultDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<StravaSyncResultDto>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _stravaService.SyncAsync(user));
        }
    }
}
