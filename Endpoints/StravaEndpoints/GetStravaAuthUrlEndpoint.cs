using System.Security.Cryptography;
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
    public class GetStravaAuthUrlEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<StravaAuthUrlResponse>
    {
        /// <summary>
        /// Name of the short-lived cookie holding the anti-CSRF state value for the OAuth flow.
        /// </summary>
        public const string StateCookieName = "strava_oauth_state";

        private readonly IStravaService _stravaService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetStravaAuthUrlEndpoint(IStravaService stravaService, UserManager<IdentityUser> userManager)
        {
            _stravaService = stravaService;
            _userManager = userManager;
        }

        [HttpGet("api/strava/auth-url")]
        [EndpointDescription("Get the Strava OAuth authorize URL to begin connecting an account.")]
        [ProducesResponseType<StravaAuthUrlResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<StravaAuthUrlResponse>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            HttpContext.Response.Cookies.Append(StateCookieName, state, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10),
                Path = "/",
            });

            var result = _stravaService.GetAuthorizeUrl(state);
            if (!result.IsSuccess)
            {
                return BadRequest(result.Errors.FirstOrDefault() ?? "Strava is not configured.");
            }

            return Ok(new StravaAuthUrlResponse(result.Value));
        }
    }
}
