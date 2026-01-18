using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Request;

namespace lionheart.Endpoints.OuraEndpoints
{
    [ValidateModel]
    public class GetDailyOuraDataRangeEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<List<DailyOuraDataDTO>>
    {
        private readonly IOuraService _ouraService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetDailyOuraDataRangeEndpoint(IOuraService ouraService, UserManager<IdentityUser> userManager)
        {
            _ouraService = ouraService;
            _userManager = userManager;
        }

        [HttpPost("api/oura/get-daily-oura-data-range")]
        [EndpointDescription("Get the daily Oura data for a user within a specified date range.")]
        [ProducesResponseType<List<DailyOuraDataDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<DailyOuraDataDTO>>> HandleAsync([FromBody] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _ouraService.GetDailyOuraInfosAsync(user, request));
        }
    }
}
