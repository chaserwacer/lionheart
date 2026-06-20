using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Strava;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.StravaEndpoints
{
    [ValidateModel]
    public class ConnectStravaEndpoint : EndpointBaseAsync
        .WithRequest<ConnectStravaRequest>
        .WithActionResult
    {
        private readonly IStravaService _stravaService;
        private readonly UserManager<IdentityUser> _userManager;

        public ConnectStravaEndpoint(IStravaService stravaService, UserManager<IdentityUser> userManager)
        {
            _stravaService = stravaService;
            _userManager = userManager;
        }

        [HttpPost("api/strava/connect")]
        [EndpointDescription("Complete the Strava OAuth flow by exchanging the authorization code for tokens.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult> HandleAsync([FromBody] ConnectStravaRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            // Validate the anti-CSRF state against the cookie set when the flow started.
            var expectedState = Request.Cookies[GetStravaAuthUrlEndpoint.StateCookieName];
            if (string.IsNullOrEmpty(expectedState) || expectedState != request.State)
            {
                return this.ToActionResult(Result.Error("Invalid or expired OAuth state. Please start the connection again."));
            }

            HttpContext.Response.Cookies.Delete(GetStravaAuthUrlEndpoint.StateCookieName);

            return this.ToActionResult(await _stravaService.ConnectAsync(user, request.Code));
        }
    }
}
