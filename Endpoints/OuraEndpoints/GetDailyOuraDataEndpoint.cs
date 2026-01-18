using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.OuraEndpoints
{
    [ValidateModel]
    public class GetDailyOuraDataEndpoint : EndpointBaseAsync
        .WithRequest<DateTime>
        .WithActionResult<DailyOuraDataDTO>
    {
        private readonly IOuraService _ouraService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetDailyOuraDataEndpoint(IOuraService ouraService, UserManager<IdentityUser> userManager)
        {
            _ouraService = ouraService;
            _userManager = userManager;
        }

        [HttpGet("api/oura/get-daily-oura-data")]
        [EndpointDescription("Get the daily Oura data for a user on a specific date.")]
        [ProducesResponseType<DailyOuraDataDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<DailyOuraDataDTO>> HandleAsync([FromQuery] DateTime request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _ouraService.GetDailyOuraInfoAsync(user, DateOnly.FromDateTime(request)));
        }
    }
}